# Exportação CSV de feedbacks (por período e projeto)

**Status: planejado.** Este documento é o plano de implementação desta feature, salvo em
`.specs/` seguindo a mesma convenção de `.specs/code-query-feedback-stats.md`.

## Contexto

A feature de feedback já cobre escrita (`POST .../code-queries/feedback`, `.specs/query-feedback.md`)
e leitura agregada semanal × projeto (`GET .../code-queries/feedback/stats`,
`.specs/code-query-feedback-stats.md`). Nenhuma das duas cobre extrair os dados brutos, linha a
linha, de `code_query_feedback` para análise externa (planilhas, BI, auditoria). O objetivo deste
trabalho é expor um endpoint que gera um arquivo CSV para download, contendo todas as informações
da tabela de feedbacks dentro de um período informado, para um projeto específico ou todos.

**Decisões confirmadas com o usuário:**
- Rota: `GET /api/v1/code-queries/feedback/export` — irmã de `/stats`, no mesmo `FeedbackController`.
- `start_date` e `end_date` são **opcionais** (query params obrigatórios não são boa prática) —
  quando ausentes, cada lado usa seu próprio default fixo, independente do outro valor informado:
  - `start_date` ausente → primeiro dia do mês corrente (00:00 UTC).
  - `end_date` ausente → agora (UTC).
  - Quando nenhum dos dois é informado, o efeito é exportar do início do mês corrente até agora.
  - Quando só um é informado, o outro lado recebe seu default acima (ex.: só `start_date` →
    `end_date` = agora; só `end_date` → `start_date` = início do mês corrente) — não há derivação
    relativa entre os dois lados, ao contrário do `/stats` (que deriva ±30 dias a partir do valor
    dado).
- `start_date` após `end_date` (após aplicar os defaults acima) é 400 (mesma regra
  `InvalidDateRange` do `/stats`).
- Limite máximo de janela: `end_date - start_date` (já com os defaults aplicados) não pode exceder
  366 dias (12 meses) — 400 se exceder (mesma regra `WindowTooLarge` do `/stats`), para evitar
  exports descontrolados.
- Filtro opcional `project_id`: quando omitido, exporta feedback de todos os projetos; quando
  informado e inexistente, 404 (mesmo padrão `ProjectNotFound` já usado em `/stats` e no POST).
- Uma linha por registro de `code_query_feedback` (sem agregação), ordenado por `created_at`
  ascendente.
- Colunas do CSV: `id, project_id, project_name, question, useful, similarities, reason, username,
  created_at`. `project_name` não existe na tabela `code_query_feedback` — é obtido via join com
  `projects`, incluído por legibilidade (mesmo padrão de `/stats`).
- `similarities` (float8[]) é serializado como uma string de array JSON dentro da célula CSV (ex.:
  `"[0.91,0.87,0.75]"`) — mesma representação usada na API JSON, entre aspas para respeitar o
  RFC 4180 (a célula contém vírgulas).
- Geração de CSV via **CsvHelper** (novo pacote NuGet, primeira dependência desse tipo no projeto)
  em vez de escrita manual — trata corretamente escaping RFC 4180 de `question`/`reason` (texto
  livre que pode conter vírgulas, aspas e quebras de linha).
- Sem tool MCP nova: endpoint de exportação/relatório para consumo humano, mesmo padrão do
  `/stats`.

## Desenho do contrato

```
GET /api/v1/code-queries/feedback/export?start_date=2026-08-04T00:00:00Z&end_date=2026-09-03T00:00:00Z&project_id=1
```

Sem `start_date`/`end_date` (ex.: `GET /api/v1/code-queries/feedback/export`, hoje 2026-09-03),
equivale a `start_date=2026-09-01T00:00:00Z&end_date=2026-09-03T<hora atual>Z` — início do mês
corrente até agora.

Resposta `200 OK`:
- `Content-Type: text/csv; charset=utf-8`
- `Content-Disposition: attachment; filename="feedback_export_20260804_20260903_project-1.csv"`
  (sufixo `_project-{id}` omitido quando `project_id` não foi informado)
- Corpo, primeira linha (header) + uma linha por feedback:

```csv
id,project_id,project_name,question,useful,similarities,reason,username,created_at
42,1,code-rag-api,"How does reranking work?",true,"[0.91,0.87,0.75]",,claude-code,2026-08-10T14:32:00Z
43,1,code-rag-api,"Where is the ""stats"" endpoint?",false,"[0.42]","Missing the join logic",jane.doe,2026-08-11T09:05:00Z
```

Falhas: `400` (`start_date` malformado, `end_date` malformado, `start_date` após `end_date` já com
defaults aplicados, janela > 366 dias já com defaults aplicados), `404` (`project_id` informado mas
inexistente), `500` (não tratado). Ver `.specs/code-query-feedback-export-openapi.yaml` (arquivo
irmão deste documento, hand-authored) para o contrato OpenAPI completo desta mudança.

## Mudanças por camada

### 1. `CodeRag.Application/Feedback` (existente)

- `FeedbackFailures`: nenhuma nova entrada — reaproveita `InvalidDateRange`, `WindowTooLarge`,
  `ProjectNotFound` já existentes (não há mais campo obrigatório ausente para validar).
- `IFeedbackService`/`FeedbackService`: novo `ExportAsync(startDate, endDate, projectId, ct)`.
  Resolve os defaults independentes por lado (`effectiveStart = startDate ??
  primeiroDiaDoMesCorrenteUtc()`, `effectiveEnd = endDate ?? DateTime.UtcNow`) — diferente de
  `GetStatsAsync`, que deriva um lado a partir do outro (±30 dias); aqui cada lado tem seu próprio
  default fixo e independente. Em seguida valida `effectiveStart <= effectiveEnd`
  (`InvalidDateRange`, 400), janela `<= 366` dias (`WindowTooLarge`, 400), e existência do projeto
  quando `projectId` informado (`ProjectNotFound`, 404). Delega ao repositório.
- Novo record de domínio `FeedbackExportRow(long Id, long ProjectId, string ProjectName, string
  Question, bool Useful, IReadOnlyList<double> Similarities, string? Reason, string Username,
  DateTime CreatedAt)`.

### 2. `CodeRag.Infrastructure.Database/Feedback` (existente)

- `IFeedbackRepository`/`FeedbackRepository`: novo `ExportAsync(startDate, endDate, projectId, ct)`.
  `SELECT f.id, f.project_id, p.name AS project_name, f.question, f.useful, f.similarities,
  f.reason, f.username, f.created_at FROM code_query_feedback f JOIN projects p ON p.id =
  f.project_id {where} ORDER BY f.created_at ASC`, onde `{where}` é montado dinamicamente via
  `WhereBuilder` (`BlogDoFT.Libs.DapperUtils.Postgres`) com `.AndWith(startDate, "f.created_at >=
  @StartDate").AndWith(endDate, "f.created_at <= @EndDate").AndWith(projectId, "f.project_id =
  @ProjectId")` — nunca `(@ProjectId::int8 IS NULL OR ...)`. O mesmo refactor foi aplicado, nesta
  mesma tarefa, a `GetStatsAsync` (filtro `project_id` da CTE `eligible_projects`) e a
  `ProjectsRepository.SearchAsync`/`NameExistsAsync`, que usavam o idiom antigo. Reaproveita o
  índice `ix_code_query_feedback_project_id_created_at` já existente — sem mudança de schema.

### 3. `CodeRag.Api` (REST)

- `CodeRag.Api.csproj`: adiciona pacote NuGet `CsvHelper`.
- `Controllers/FeedbackController.cs`: nova ação `GET("export")`. Query params `start_date`/
  `end_date` como `DateTimeOffset?` (mesmo motivo do `/stats`: evitar ambiguidade de `Kind` no
  binding), `project_id` como `long?`. Constrói o CSV via `CsvWriter` (CsvHelper) a partir das
  `FeedbackExportRow` e retorna `File(bytes, "text/csv", filename)` com `Content-Disposition`
  setado explicitamente. Sem novos `Contracts` JSON — a resposta é CSV puro, não há DTO de
  serialização JSON.

### 4. Schema

Sem mudanças — reaproveita `ix_code_query_feedback_project_id_created_at` (já criado para o
`/stats`). Sem alteração em `db/init.sql` nem nos `Schema.cs` de teste.

### 5. Testes

- `FeedbackServiceTests`: nenhum parâmetro informado (default = início do mês corrente até agora),
  só `start_date` informado (`end_date` = agora), só `end_date` informado (`start_date` = início do
  mês corrente), `start_date > end_date` (já com defaults aplicados) → 400, janela > 366 dias
  (já com defaults aplicados) → 400, `project_id` inexistente → 404, caminho feliz (rows corretas,
  ordenadas por `created_at`).
- `FeedbackRepositoryTests` (Testcontainers): filtro por janela (feedback fora da janela
  excluído), filtro por `project_id`, join correto de `project_name`, ordenação ascendente por
  `created_at`.
- `CodeQueryFeedbackExportEndpointTests` (WebApplicationFactory, novo): `200` com
  `Content-Type`/`Content-Disposition` corretos e corpo CSV válido (incluindo um caso com
  `question`/`reason` contendo vírgula/aspas/quebra de linha, para validar o escaping do
  CsvHelper), `400` para cada caso de validação, `404` para `project_id` inexistente.

### 6. Docs

- `.specs/code-query-feedback-export-openapi.yaml` (novo, hand-authored, ao lado deste documento).
- `openapi.yaml` (canônico): regenerado via Swashbuckle CLI ao final da implementação.
- `README.md`: sem mudança (nenhuma tool MCP nova).

## Verificação (a fazer durante a implementação)

`dotnet test` completo (todos os projetos); smoke test manual passo a passo (múltiplos projetos,
feedback espalhado em datas diferentes dentro/fora da janela, filtro `project_id` presente e
ausente, download do CSV e conferência de colunas/escaping/ordenação, casos 400 de cada validação,
404 de projeto inexistente); regeneração do `openapi.yaml` via Swashbuckle CLI.
