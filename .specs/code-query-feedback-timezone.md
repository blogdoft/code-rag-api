# Exibição localizada de created_at no export de feedback

**Status: em implementação.** Este documento é o plano de implementação desta feature, salvo em
`.specs/` seguindo a mesma convenção de `.specs/code-query-feedback-export.md`.

**⚠️ Correção em relação ao plano original:** a primeira versão deste documento diagnosticava uma
suposta corrupção de `created_at` em `projects`, `embedding_models` e `code_query_feedback` (via
`SystemMethods.CurrentUTCDateTime`/`now() AT TIME ZONE 'UTC'` gerando um `DEFAULT` sensível ao
timezone da sessão do Postgres) e planejava uma migration em `code-indexer` com backfill. Ao validar
essa migration contra um Postgres descartável, ficou provado que **Npgsql (9.0.5, usado tanto por
`code-rag-api` quanto por `code-indexer`) força a sessão para UTC automaticamente ao conectar**,
independente do timezone padrão do servidor - então o `DEFAULT` problemático nunca é de fato
acionado pelo caminho de escrita real da aplicação. Testes diretos contra produção confirmaram:
inserir um feedback agora e comparar com o relógio real bateu em segundos (não em 3h), e
`projects.created_at` de linhas conhecidas bate com a linha do tempo real. **Não há corrupção de
dado.** O sintoma relatado ("preciso incluir o dia de amanhã para ver dados de hoje" ao filtrar no
DBeaver) é o efeito normal e esperado de olhar `created_at` em UTC enquanto se filtra pensando no
calendário local (`America/Sao_Paulo`) - um evento das 22h de hoje em SP já é `01:00 UTC` de amanhã.
A migration em `code-indexer` foi descartada (não commitada). O que resta - e o que este documento
agora descreve - é só a parte de exibição: um parâmetro opcional para renderizar `created_at` no
fuso horário de quem está olhando.

## Contexto

`created_at` é armazenado corretamente em UTC (confirmado). O problema real é só de leitura humana:
uma pessoa olhando o CSV de export (ou uma ferramenta de banco como o DBeaver) pensa em termos do
seu calendário local, não UTC, e "hoje" ou "esta semana" não batem visualmente com o que está
armazenado. Além disso, "horário do Brasil" não é um offset único: o país tem 4 fusos oficiais desde
o fim do horário de verão em 2019 (UTC-2 Fernando de Noronha, UTC-3 a maior parte do país, UTC-4
Amazonas/Mato Grosso, UTC-5 Acre) - qualquer solução de exibição precisa deixar o fuso explícito por
quem consome, não assumir um único offset.

**Decisões confirmadas com o usuário:**
- Armazenamento e contrato de API (JSON, MCP) continuam sendo UTC, sempre - um instante absoluto e
  inequívoco. Localização é responsabilidade da borda de exibição, escolhida explicitamente (nome
  IANA), nunca inferida do locale do servidor.
- `GET /feedback/export` ganha um parâmetro opcional `timezone` (nome IANA). Quando informado, a
  célula `created_at` do CSV é renderizada no horário local daquele fuso com offset explícito (ex.
  `2026-09-03T20:47:20-03:00`); quando omitido, comportamento atual mantido (UTC, sufixo `Z`).
- `timezone` afeta **apenas** a coluna `created_at` exibida no CSV - não os limites de filtro
  (`start_date`/`end_date`) nem os defaults de janela ("início do mês corrente" continua UTC).
- `/stats` não recebe nenhuma mudança - fora de escopo, já que os dados nunca estiveram corrompidos.
- `code-rag-front` ganha uma configuração de timezone (IANA, default `America/Sao_Paulo`) na página
  de settings; qualquer chamada de API que aceite `timezone` usa esse valor como default.
- Nenhuma mudança em `code-indexer` ou no schema - não há causa raiz de banco a corrigir.

## Desenho do contrato

Ver `.specs/code-query-feedback-timezone-openapi.yaml` (arquivo irmão deste documento,
hand-authored, CHANGE ONLY sobre o endpoint já existente `GET .../feedback/export`) para o contrato
completo desta mudança - é a fonte de verdade que tanto o backend quanto o front devem seguir.

Resumo: novo parâmetro de query opcional `timezone` (string, nome IANA, ex.
`America/Sao_Paulo`). Ausente → `created_at` no formato atual (`...Z`, UTC). Presente e válido →
`created_at` formatado com o offset local daquele fuso (`yyyy-MM-ddTHH:mm:sszzz`). Presente e
inválido (não reconhecido como IANA) → `400`, mesmo formato de erro (`application/problem+json`,
`ProblemDetails`) já usado pelas outras validações do endpoint.

## Mudanças por camada

### 1. `code-rag-api` (este repo) - deve seguir `code-query-feedback-timezone-openapi.yaml`

- `src/CodeRag.Application/Feedback/FeedbackFailures.cs`: nova falha `InvalidTimezone(string
  timezone)` (400).
- `src/CodeRag.Api/Controllers/FeedbackController.cs`: ação `ExportAsync` ganha
  `[FromQuery(Name = "timezone")] string? timezone` - nome, tipo, opcionalidade e mensagens de erro
  exatamente como especificado em `.specs/code-query-feedback-timezone-openapi.yaml`. Validação via
  `TimeZoneInfo.TryFindSystemTimeZoneById`. Quando válido, converte `row.CreatedAt` (UTC) via
  `TimeZoneInfo.ConvertTime(new DateTimeOffset(row.CreatedAt, TimeSpan.Zero), tz)` e formata com
  offset explícito (`yyyy-MM-ddTHH:mm:sszzz`); caso contrário mantém o formato atual (`Z` fixo).
- `Dockerfile`: verificar na implementação se `tzdata` já está presente em
  `mcr.microsoft.com/dotnet/aspnet:9.0`; se `FindSystemTimeZoneById` falhar em runtime, adicionar
  `RUN apt-get update && apt-get install -y --no-install-recommends tzdata` no estágio `runtime`.
- `openapi.yaml` (canônico): regenerado via Swashbuckle CLI ao final da implementação - deve bater
  com `.specs/code-query-feedback-timezone-openapi.yaml`.
- Testes: `FeedbackServiceTests`/`CodeQueryFeedbackExportEndpointTests` - timezone válido renderiza
  offset correto, timezone inválido → 400, omitido mantém comportamento atual (UTC/`Z`).

### 2. `code-rag-front` (repo irmão) - deve seguir `code-query-feedback-timezone-openapi.yaml`

O front já tem um spec planejado (não implementado) para o botão de export CSV:
`.specs/2026-09-03-feedback-csv-export.md` nesse repo, hoje só com `start_date`/`end_date`/
`project_id` - precisa ganhar o `timezone`, com nome e semântica exatamente como especificado em
`code-rag-api`'s `.specs/code-query-feedback-timezone-openapi.yaml`.

- `src/app/core/services/config.service.ts`: novo par `EXPORT_TIMEZONE_KEY =
  'code-rag.exportTimezone'` + default `'America/Sao_Paulo'`, seguindo o padrão já usado por
  `apiBaseUrl`/`userName` (signal privado lido do `localStorage`, exposto `.asReadonly()`,
  `setExportTimezone(value)` público que faz trim e persiste).
- `src/app/features/settings/settings-page.ts` + `.html`: novo campo de texto "Timezone (IANA)",
  mesmo padrão dos campos existentes (signal local seedado do service, `save()` valida e chama
  `configService.setExportTimezone(...)`, toast de sucesso/erro).
- `FeedbackStatsService.exportCsv()` (a criar junto com o spec de CSV export do front) usa
  `configService.exportTimezone()` como valor default do parâmetro `timezone` do `HttpParams`,
  mesmo padrão condicional já usado para `start_date`/`project_id` em `feedback-stats.service.ts`.
- Atualizar `.specs/2026-09-03-feedback-csv-export.md` (nesse repo) para incluir o parâmetro
  `timezone`.
- Testes: `config.service.spec.ts` (default/leitura/trim/persistência do novo campo) e
  `settings-page.spec.ts` (novo campo renderiza, salva). Teste de `exportCsv()` cobrindo o
  `timezone` default vindo do `ConfigService` mockado.

## Verificação

- `dotnet test` completo em `code-rag-api` (endpoints).
- Smoke test manual: `GET /api/v1/code-queries/feedback/export` sem `timezone` (igual a hoje, `Z`),
  com `timezone=America/Sao_Paulo` (offset `-03:00`) e com um IANA inválido (400).
- `code-rag-front`: `npm test` (Vitest) cobrindo os specs novos; manual - configurar um timezone na
  página de settings, exportar o CSV, conferir que `created_at` vem no offset esperado sem precisar
  digitar `timezone` na URL manualmente.
