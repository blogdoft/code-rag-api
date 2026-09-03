# Feedback de efetividade das code-queries

**Status: implementado.** Este documento é o plano de implementação desta feature, salvo em
`.specs/` seguindo a mesma convenção de `.specs/code-queries-filters.md` e `.specs/reranking.md`.

## Contexto

A API expõe hoje `POST /api/v1/projects/{projectId}/code-queries`, que devolve resultados de
busca semântica (com `similarity`/`rerankScore`), mas não existe nenhuma forma de saber se esses
resultados foram realmente úteis. O objetivo deste trabalho é permitir contabilizar a
efetividade das perguntas feitas ao RAG: criar um endpoint de **submissão** de feedback (humano
via REST, IA via MCP) que registra a pergunta feita, se a resposta foi útil, os valores de
similaridade que foram retornados (apenas os `similarity`, não `rerankScore`) e, opcionalmente,
por que não foi útil. Todo caller — humano ou agente — precisa se identificar via um campo
`user` obrigatório; para chamadas MCP, isso significa que o próprio agente (Claude Code, Codex,
CrewAI, Hermes, OpenCode, etc.) deve se auto-identificar explicitamente a cada chamada — não há
como confiar no `clientInfo` do handshake MCP porque este servidor roda em modo HTTP stateless.

**Decisões confirmadas com o usuário:**
- Nenhum endpoint de leitura/listagem é criado nesta iteração — os dados serão consultados
  diretamente no Postgres. Apenas o caminho de escrita (POST) é implementado.
- Como a nova tabela `code_query_feedback` tem uma FK `NOT NULL` para `projects` (mesmo estilo
  sem `ON DELETE` de `code_documents`), `DELETE /api/v1/projects/{projectId}` também ganha uma
  pré-checagem para feedback existente, espelhando o padrão já usado hoje para `code_documents`
  (409 limpo em vez de erro de FK cru/500).
- A coluna é chamada `username` (não `user`, palavra problemática em SQL cru), mas o campo
  JSON/API-facing permanece `user`.

## Desenho do contrato

Rota nova, aninhada no controller de code-queries existente (é fortemente acoplada a ele):

```
POST /api/v1/projects/{projectId}/code-queries/feedback
```

Corpo da requisição (JSON, snake_case):
```json
{
  "question": "where is the retry logic for failed payments?",
  "useful": true,
  "similarities": [0.83, 0.71, 0.65],
  "reason": null,
  "user": "ftathiago"
}
```

Resposta `201 Created` (sem header `Location` — não existe rota GET-by-id para feedback nesta
iteração; desvio intencional do padrão de `ProjectsController.CreateAsync`):
```json
{
  "id": 1,
  "project_id": 1,
  "question": "where is the retry logic for failed payments?",
  "useful": true,
  "similarities": [0.83, 0.71, 0.65],
  "reason": null,
  "user": "ftathiago",
  "created_at": "2026-09-03T12:00:00Z"
}
```

Falhas 400 (uma por campo obrigatório/limite), 404 se o projeto não existir, 500 não tratado. Ver
`.specs/openapi.query-feedback.yaml` (arquivo irmão deste documento, hand-authored) para o
contrato OpenAPI completo desta mudança, incluindo a resposta 409 adicional em
`DELETE /api/v1/projects/{projectId}`.

## Mudanças por camada

### 1. `CodeRag.Application/Feedback` (novo)

- `IFeedbackService`/`FeedbackService`: `SubmitAsync(projectId, question, useful, similarities, reason, user, ct)`.
  Constantes: `MaxQuestionLength = 1000`, `MaxUserLength = 200`, `MaxReasonLength = 1000`,
  `MaxSimilaritiesCount = 50`. Ordem de validação: `question` → `useful` (`bool?`, null =
  ausente) → `similarities` (`null` = ausente; lista vazia é válida) → `user` → `reason`
  (opcional) → existência do projeto (`ProjectNotFound`, 404).
- `FeedbackResult`: record de domínio espelhando a tabela.
- `FeedbackFailures`: `QuestionRequired`, `QuestionTooLong`, `UsefulRequired`,
  `SimilaritiesRequired`, `TooManySimilarities`, `UserRequired`, `UserTooLong`,
  `ReasonTooLong` (400), `ProjectNotFound` (404) — mesmo padrão de `Code` prefixado com status
  HTTP de `CodeQueryFailures`/`ProjectFailures`.
- `IFeedbackRepository`: `InsertAsync(...)` + `ExistsForProjectAsync(projectId, ct)` (usado pela
  checagem de delete de projeto, item 5).

### 2. `CodeRag.Infrastructure.Database/Feedback` (novo)

- `FeedbackRepository`: `InsertAsync` via Dapper `INSERT ... RETURNING`, mesmo padrão de
  `ProjectsRepository.InsertAsync`. `similarities` é `double[]`/`float8[]` — primeiro uso de
  coluna array no repo; Npgsql/Dapper mapeiam o valor nativamente, sem configuração extra.
  `ExistsForProjectAsync`: `SELECT EXISTS(...)`, mesmo padrão de `ProjectsRepository.ExistsAsync`.

  **⚠️ Correção em relação ao plano original:** a premissa inicial era usar um `private sealed
  record FeedbackRow(...)` posicional, igual a `ProjectRow`/`FeedbackRow` em outros repositórios.
  Isso **falhou em runtime** (não em compile-time) com
  `InvalidOperationException: A parameterless default constructor or one matching signature (...)
  is required for ... FeedbackRow materialization`, só nos testes que de fato inserem uma linha
  (Testcontainers e a suíte HTTP). Causa raiz: o caminho de fast-path de "constructor matching" do
  Dapper para records exige que o tipo de cada parâmetro do construtor seja *exatamente* igual ao
  tipo que `reader.GetFieldType(i)` reporta para a coluna; para uma coluna `float8[]`, o Npgsql
  reporta o tipo genérico `System.Array` nesse metadado (não `System.Double[]`), mesmo que o valor
  real devolvido em runtime seja um `double[]` de verdade — isso quebra o casamento de construtor
  mesmo sem nenhuma incompatibilidade real de dado. Correção: `FeedbackRow` deixou de ser um
  record posicional e virou uma classe mutável comum com propriedades `{ get; set; }` — o caminho
  de mapeamento por propriedade do Dapper não tem essa exigência de correspondência exata de tipo
  do `GetFieldType`. Isso gera falsos positivos do Sonar (`S3459`/`S1144`, "propriedade não
  atribuída"/"setter não usado", já que o Dapper popula via reflection, invisível ao analisador),
  suprimidos com `#pragma warning disable/restore S3459, S1144` em torno da classe.

### 3. `CodeRag.Api` (REST)

- `Contracts/CodeQueryFeedbackRequest.cs` / `CodeQueryFeedbackResponse.cs`.
- `CodeQueriesController`: nova ação `SubmitFeedbackAsync` em `[HttpPost("feedback")]`, injeta
  `IFeedbackService`. Retorna 201 sem `Location` (documentado em `<remarks>`).

### 4. `CodeRag.Mcp/Tools/CodeQueryTools.cs`

- Novo `CodeQueryFeedbackToolResult` (espelha a resposta REST completa, mesmo padrão de
  `ProjectToolResult`/`CodeQueryToolResult`).
- Novo tool `submit_code_query_feedback`, injeta `IFeedbackService`. `[Description]` instrui o
  agente a chamar depois de `query_project_code`, devolver os `similarity` exatos recebidos, e
  se identificar via `user` (exemplos: "claude code", "codex", "crewai", "hermes", "opencode").
  `user` sem default → o SDK MCP reflete como campo obrigatório no schema da tool.
- `query_project_code`'s `[Description]` ganha uma frase final encadeando para a nova tool,
  espelhando a referência que `list_projects` já faz a `query_project_code`.

### 5. `CodeRag.Application/Projects` — checagem de delete (escopo extra confirmado)

- `ProjectFailures.HasFeedback(projectId)` → `409-has-feedback`.
- `ProjectsService.DeleteAsync` injeta `IFeedbackRepository` e checa
  `feedbackRepository.ExistsForProjectAsync` depois da checagem existente de
  `codeDocumentsRepository.ExistsForProjectAsync`, retornando `HasFeedback` antes do delete real.

### 6. Schema (3 lugares mantidos em sincronia)

- `db/init.sql`, `tests/CodeRag.Api.Tests/Schema.cs`, `tests/CodeRag.Infrastructure.Database.Tests/Schema.cs`:
  nova tabela `public.code_query_feedback` (`id`, `project_id` FK, `question`, `useful`,
  `similarities float8[]`, `reason`, `username`, `created_at`), índice em `project_id`.

### 7. DI

- `CodeRag.Application/ServiceCollectionExtensions.cs`: `AddScoped<IFeedbackService, FeedbackService>()`.
- `CodeRag.Infrastructure.Database/ServiceCollectionExtensions.cs`: `AddScoped<IFeedbackRepository, FeedbackRepository>()`.

### 8. Testes

- `FeedbackServiceTests`, `FeedbackRepositoryTests` (Testcontainers), `CodeQueryFeedbackEndpointTests`
  (WebApplicationFactory — este endpoint não chama o provedor de embeddings, então um 201 real é
  alcançável, diferente dos testes de `code-queries`), passthrough em `CodeQueryToolsTests`.
- `ProjectsServiceTests`/`ProjectsEndpointTests`: novo caso de 409 ao deletar projeto com
  feedback registrado.

### 9. Docs

- `README.md`: nova entrada para `submit_code_query_feedback` na lista de tools MCP.
- `openapi.yaml` (canônico, gerado): regenerado ao final via Swashbuckle, mesma abordagem
  documentada em `.specs/code-queries-filters.md` (seção 5).
- `.specs/openapi.query-feedback.yaml` (novo, hand-authored, ao lado deste documento): documenta
  isoladamente as duas mudanças de contrato desta spec (o novo `POST .../code-queries/feedback`
  completo, e a resposta 409 adicional de `DELETE /api/v1/projects/{projectId}`) — material de
  revisão da mudança de contrato, não usado no build.

## Verificação feita

- `dotnet test` no solution inteiro: **todas as suítes verdes** (`CodeRag.Application.Tests`:
  78, `CodeRag.Infrastructure.Database.Tests`: 40, `CodeRag.Api.Tests`: 51, `CodeRag.Mcp.Tests`:
  10, mais as demais suítes pré-existentes de embeddings/reranking inalteradas), 0 falhas. Nenhum
  teste pré-existente regrediu.
- Build local limpo (só os warnings pré-existentes do bug conhecido do analisador StyleCop em
  outros projetos do repo, e o `S6960` pré-existente sobre `CodeQueriesController` ter múltiplas
  responsabilidades — não relacionado a este trabalho).
- Smoke test REST manual, contra o Postgres de desenvolvimento (`docker compose up -d postgres`,
  aplicando a DDL da nova tabela manualmente via `psql` já que o volume de dados já existia e
  `docker-entrypoint-initdb.d` só roda em volume novo) com a API rodando localmente
  (`dotnet run`, `Embeddings__BaseUrl` apontando para um endereço não roteável de propósito, já
  que este endpoint nunca chama o provedor de embeddings):
  1. `POST /api/v1/projects` → 201, id do projeto obtido.
  2. `POST /api/v1/projects/{id}/code-queries/feedback` com corpo válido → **201**, sem header
     `Location`, corpo ecoando `id`/`project_id`/`question`/`useful`/`similarities`/`reason:
     null`/`user`/`created_at` exatamente como enviado.
  3. Mesma chamada sem `user` → **400**, `detail` confirma
     `"The 'user' field is required and must not be empty. For MCP callers, this must be the
     calling agent/tool's own name."`.
  4. Mesma chamada com `projectId` inexistente (999999999) → **404**.
  5. `DELETE /api/v1/projects/{id}` após o feedback acima → **409**, `detail`:
     `"Project 2 has feedback records and cannot be deleted. Remove its feedback records first."`.
  6. Linha conferida diretamente via `psql` em `public.code_query_feedback` — presente e
     consistente com o payload enviado. Dados de teste removidos ao final (`DELETE` direto na
     tabela + no projeto), container Postgres iniciado para o teste foi parado/removido
     preservando o volume de dados persistente.
- `openapi.yaml` (canônico) regenerado via Swashbuckle CLI (`swashbuckle.aspnetcore.cli` 10.2.3,
  rodando o `.dll` net9.0 diretamente) — inclui o novo path
  `POST /api/v1/projects/{projectId}/code-queries/feedback`, os schemas
  `CodeQueryFeedbackRequest`/`CodeQueryFeedbackResponse`, e a descrição atualizada do 409 de
  `DELETE /api/v1/projects/{projectId}` mencionando feedback. Validado como YAML válido.
- Smoke test via MCP **não foi feito nesta sessão**: o servidor MCP conectado à sessão atual roda
  um build anterior a este trabalho (sem a tool `submit_code_query_feedback`); validar exigiria
  reiniciar esse servidor a partir do código atualizado, fora do escopo desta verificação
  (mesma limitação já registrada em `.specs/code-queries-filters.md`).
