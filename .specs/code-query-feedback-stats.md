# Estatísticas de efetividade do feedback (por semana e por projeto)

**Status: em implementação.** Este documento é o plano de implementação desta feature, salvo em
`.specs/` seguindo a mesma convenção de `.specs/query-feedback.md`.

## Contexto

A feature de feedback (`.specs/query-feedback.md`, implementada em `f176c12`) deliberadamente só
implementou o caminho de **escrita** — `POST /api/v1/projects/{projectId}/code-queries/feedback`
— deixando explícito que "os dados serão consultados diretamente no Postgres" nesta primeira
iteração. Já se acumulou feedback suficiente na tabela `public.code_query_feedback` para que essa
consulta manual deixe de ser prática: o objetivo deste trabalho é expor um endpoint de **leitura
agregada** que devolva, dentro de uma janela de tempo configurável (até 12 meses), quantos
feedbacks foram úteis vs. não úteis, quebrados por semana e, dentro de cada semana, por projeto.
Este é o primeiro endpoint de leitura/agregação sobre feedback nesta API.

**Decisões confirmadas com o usuário:**
- Rota: `GET /api/v1/code-queries/feedback/stats` — irmã da rota de escrita existente, mas sem
  `{projectId}` no path (já que agrupa por múltiplos projetos), em um controller novo e dedicado
  (`FeedbackController`), em vez de crescer ainda mais o `CodeQueriesController` (que já carrega o
  warning pré-existente Sonar S6960 de responsabilidade múltipla).
- Janela de tempo (`start_date`/`end_date`), ambos opcionais e independentes:
  - Nenhum informado → últimos 30 dias (`end_date = agora UTC`, `start_date = end_date - 30 dias`).
  - Só `start_date` → janela de `start_date` até `start_date + 30 dias`.
  - Só `end_date` → janela de `end_date - 30 dias` até `end_date`.
  - Ambos informados → usa exatamente o intervalo dado; `start_date` após `end_date` é 400.
  - Limite máximo: a janela efetiva (`end_date - start_date`) não pode exceder 366 dias (12
    meses); se exceder, 400.
- Agrupamento em dois níveis: primeiro por semana ISO (segunda a domingo, alinhada ao calendário
  — não blocos de 7 dias a partir de `start_date`), depois por projeto dentro de cada semana.
- Grade densa (cross join), ambos os níveis sempre completos:
  - Toda semana que se sobrepõe à janela aparece na resposta, mesmo sem nenhum feedback de
    nenhum projeto naquela semana.
  - Dentro de cada semana, todos os projetos cadastrados aparecem (ou só o projeto filtrado por
    `project_id`, se informado), mesmo com contagem 0 naquela semana específica.
  - Útil para preencher um eixo X contínuo em gráficos sem buracos.
- Filtro opcional `project_id`: restringe a lista de projetos (em cada semana) a um único
  projeto; projeto inexistente → 404 (mesmo padrão de `ProjectNotFound` já usado no POST de
  feedback).
- Sem tool MCP nova: é um endpoint de relatório/dashboard para consumo humano, não parte do fluxo
  de consulta de código que os agentes MCP já usam.

## Desenho do contrato

```
GET /api/v1/code-queries/feedback/stats?start_date=2026-08-04T00:00:00Z&end_date=2026-09-03T00:00:00Z&project_id=1
```

Resposta `200 OK`:
```json
{
  "start_date": "2026-08-04T00:00:00Z",
  "end_date": "2026-09-03T00:00:00Z",
  "weeks": [
    {
      "week_start": "2026-08-03",
      "week_end": "2026-08-09",
      "projects": [
        {
          "project_id": 1,
          "project_name": "code-rag-api",
          "total_count": 5,
          "useful_count": 4,
          "not_useful_count": 1,
          "useful_percentage": 80.0,
          "not_useful_percentage": 20.0
        }
      ]
    }
  ]
}
```

`start_date`/`end_date` no nível raiz são sempre a janela efetiva calculada pela regra acima
(mesmo quando o cliente não informou nada). `week_start`/`week_end` são datas (sem hora), limites
reais do calendário ISO da semana — não recortados pela janela pedida. Percentuais arredondados
para 2 casas decimais; `total_count == 0` → ambos os percentuais são `0.0`.

Falhas 400 (data malformada, `start_date` após `end_date`, janela > 366 dias), 404 (`project_id`
inexistente), 500 (não tratado). Ver `.specs/code-query-feedback-stats-openapi.yaml` (arquivo
irmão deste documento, hand-authored) para o contrato OpenAPI completo desta mudança.

## Mudanças por camada

### 1. `CodeRag.Application/Feedback` (existente)

- `IFeedbackService`/`FeedbackService`: novo `GetStatsAsync(startDate, endDate, projectId, ct)`.
  Resolve a janela efetiva (regra dos 30 dias), valida `startDate <= endDate` quando ambos
  informados (`InvalidDateRange`, 400), valida janela efetiva `<= 366` dias (`WindowTooLarge`,
  400), checa existência do projeto quando `projectId` informado (`ProjectNotFound`, 404), e
  delega a agregação ao repositório.
- Novos records de domínio: `FeedbackStatsResult`, `WeeklyFeedbackStats`, `ProjectFeedbackStats`.
- `FeedbackFailures`: `InvalidDateRange`, `WindowTooLarge` (400).

### 2. `CodeRag.Infrastructure.Database/Feedback` (existente)

- `IFeedbackRepository`/`FeedbackRepository`: novo `GetStatsAsync(startDate, endDate, projectId, ct)`.
  Gera a grade densa semana × projeto no SQL via `generate_series` (semanas ISO, `date_trunc('week', ...)`
  trunca para a segunda-feira no Postgres) cruzado (`CROSS JOIN`) com os projetos elegíveis, e
  `LEFT JOIN` do feedback casando semana e projeto. Percentuais calculados no mapeamento C#
  (`Math.Round(..., 2)`, 0 quando `total_count == 0`).

### 3. `CodeRag.Api` (REST)

- `Contracts/CodeQueryFeedbackStatsResponse.cs`, `WeeklyFeedbackStatsResponse.cs`,
  `ProjectFeedbackStatsResponse.cs` (novos).
- `Controllers/FeedbackController.cs` (novo controller): `[Route("api/v1/code-queries/feedback")]`,
  `[ApiExplorerSettings(GroupName = "Code Query")]`, `GET /stats`. Query params `start_date`/
  `end_date` recebidos como `DateTimeOffset?` (não `DateTime?`, para evitar ambiguidade de `Kind`
  no binding de strings com sufixo `Z`/offset) e convertidos para UTC antes de chamar o service.

### 4. Schema — índice (sem nova tabela/coluna)

`db/init.sql`: substitui `ix_code_query_feedback_project_id` por um índice composto
`ix_code_query_feedback_project_id_created_at ON (project_id, created_at)` — a nova consulta
sempre filtra `created_at` dentro do grupo de `project_id`, e o prefixo `project_id` sozinho
continua servindo `ExistsForProjectAsync`. Os schemas de teste (`tests/CodeRag.Api.Tests/Schema.cs`,
`tests/CodeRag.Infrastructure.Database.Tests/Schema.cs`) não declaram índices hoje, então não
precisam de mudança.

### 5. Testes

- `FeedbackServiceTests`: janela padrão, só `start_date`, só `end_date`, ambos informados,
  `start_date > end_date` → 400, janela > 366 dias → 400, `project_id` existente/inexistente.
- `FeedbackRepositoryTests` (Testcontainers): grade densa (semana/projeto sem feedback aparece
  zerado), feedback fora da janela excluído, contagem correta por semana ISO, filtro `project_id`.
- `CodeQueryFeedbackStatsEndpointTests` (WebApplicationFactory, novo): 200 com/sem params, 400
  range inválido, 400 janela grande demais, 404 `project_id` inexistente.

### 6. Docs

- `.specs/code-query-feedback-stats-openapi.yaml` (novo, hand-authored, ao lado deste documento).
- `openapi.yaml` (canônico): regenerado via Swashbuckle CLI ao final da implementação.
- `README.md`: sem mudança (nenhuma tool MCP nova).

## Verificação

Ver seção "Verificação" do plano de implementação original — cobre `dotnet test` completo e um
smoke test manual passo a passo (2 projetos, feedback espalhado em múltiplas semanas ISO,
validação da grade densa, filtro `project_id`, casos de 400/404).
