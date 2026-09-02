# Reranking opcional do pipeline de busca por código

**Status: implementado.**

## Contexto

O pipeline de consulta atual (`CodeQueryService.QueryAsync`) ordena os resultados
exclusivamente pela similaridade de cosseno entre o embedding da pergunta e o embedding de
cada trecho de código, calculada pelo pgvector. Isso é bom para recall, mas fraco para
precisão em código legado: nomenclatura e comportamento divergem do que os modelos de
embedding viram no treino, e "near-misses" estruturais (sintaxe parecida, propósito
diferente) acabam ranqueados alto. Um estágio de reranking — reavaliar os top-N candidatos
da busca vetorial com lógica mais cara e sensível à query — melhora a precisão do topo da
lista antes dela ser entregue ao consumidor via API/MCP, o que importa especialmente quando
só cabem 5-10 trechos na janela de contexto de um agente.

O pedido foi adicionar reranking como um **estágio de pipeline totalmente opcional,
parametrizável por configuração**, que:
- vem desligado por padrão, sem qualquer mudança de comportamento/performance;
- funciona contra uma instância Ollama local/self-hosted do próprio usuário (Ollama não tem
  endpoint nativo de cross-encoder/rerank, então usa-se **scoring pointwise via prompt de
  LLM**: uma chamada por candidato pedindo a um modelo de chat/instruct uma nota de
  relevância 0-10, com concorrência limitada);
- é arquitetado para que um segundo backend hospedado (estilo Cohere Rerank) possa ser
  adicionado sem qualquer mudança no resolver — um stub para isso foi incluído já, mesmo o
  Ollama sendo o único backend efetivamente configurado por padrão.

O desenho espelha quase exatamente a abstração de provedores de embedding já existente
(`CodeRag.Embeddings.Abstraction` + `.Local`/`.Ollama`/`.OpenAI`), com uma divergência
deliberada: embeddings são obrigatórios e falham rápido se mal configurados; reranking é
opcional, então "não configurado" resolve para um passthrough em vez de derrubar o startup.

Levantamento feito antes de implementar: não existia nenhum reranking, busca híbrida/BM25,
nem um padrão de feature flag booleana em nenhum lugar do repo (confirmado por grep
completo). O precedente mais próximo é exatamente a abstração de embeddings citada acima —
`IEmbeddingGenerator`/`IEmbeddingProviderFactory`/`EmbeddingGeneratorResolver`/
`EmbeddingOptions` — cujo padrão (interface + factory por provedor + resolver + options) foi
reproduzido aqui.

## Desenho

### 1. `CodeRag.Reranking.Abstraction`

- **`IReranker`**: `Provider` (nome), `CandidatePoolSize` (quantos resultados da busca
  vetorial buscar antes de truncar para o limite do chamador; `0` quando desligado),
  `RerankAsync(string query, IReadOnlyList<RerankCandidate> candidates, CancellationToken)`
  → `IReadOnlyList<RerankedCandidate>`.
- **`RerankCandidate(long Id, string Text)`** / **`RerankedCandidate(long Id, double? Score)`**
  — deliberadamente desacoplados de `CodeQueryResult` (que vive em `CodeRag.Application`)
  para evitar referência circular entre projetos, do mesmo jeito que `IEmbeddingGenerator`
  trabalha com `string` puro em vez de um tipo de domínio.
- **`RerankingOptions`**, seção de config `"Reranking"`: `Provider` (vazio/`"None"` =
  desligado), `Model`, `BaseUrl`, `ApiKey`, `TimeoutSeconds` (30), `CandidatePoolSize` (25),
  `MaxConcurrency` (4).
- **`IRerankerProviderFactory`**: `ProviderName` + `Create(RerankingOptions)`.
- **`NoOpReranker`** (internal): devolve cada candidato inalterado, na ordem original, com
  `Score = null`. É isso que permite ao `CodeQueryService` chamar `IReranker`
  incondicionalmente, sem nenhum `if (reranking habilitado)`.
- **`RerankerResolver`**: dicionário de factories por `ProviderName` (case-insensitive).
  Diferente do `EmbeddingGeneratorResolver`: `Provider` vazio ou `"None"` resolve para
  `NoOpReranker` em vez de lançar exceção (desligado é uma configuração válida, não um erro);
  um nome de provider não-vazio mas desconhecido continua lançando
  `InvalidOperationException` no startup (erro de digitação ainda falha rápido).
- **`RerankingException`** — espelha `EmbeddingGenerationException` (falha de
  infraestrutura, não modelada como `Failure`, vira 500).
- **`ServiceCollectionExtensions.AddRerankingAbstraction(configuration)`** — espelha
  `AddEmbeddingAbstraction`.

### 2. `CodeRag.Reranking.Ollama` — estratégia pointwise

Para cada candidato (limitado por `SemaphoreSlim` dimensionado por
`RerankingOptions.MaxConcurrency`), chama `POST api/generate` do Ollama com saída
estruturada (JSON Schema `{"score": integer 0-10}`, `temperature: 0`) e um prompt contendo a
pergunta e o `Text` do candidato. A nota é normalizada para `[0.0, 1.0]` (mesma escala de
`Similarity`) e os candidatos são ordenados por nota decrescente. Falha ao pontuar qualquer
candidato lança `RerankingException` (vira 500) — sem degradação parcial nesta primeira
versão, mesma convenção de "falhas de infra propagam cru" já usada em
`CodeQueryService.cs` para embeddings.

- `OllamaReranker` — recebe `HttpClient` + `RerankingOptions` via construtor, mesmo formato
  de `OllamaEmbeddingGenerator`.
- `OllamaRerankerProviderFactory` — lança se `BaseUrl` estiver em branco.
- `ServiceCollectionExtensions.AddOllamaRerankerProvider()` — HTTP client nomeado
  `"OllamaReranker"`, **distinto** do client `"Ollama"` usado por embeddings.
- Exige um modelo de chat/instruct já baixado na instância Ollama alvo (ex.
  `qwen2.5:7b-instruct`) — `bge-m3` (usado para embeddings) é só-embedding e não serve aqui.

### 3. `CodeRag.Reranking.Cohere` — stub registrado, não usado por padrão

A API de Rerank da Cohere (`POST v2/rerank`) é nativamente listwise: uma única chamada com
`model`/`query`/`documents`/`top_n` pontua todos os candidatos de uma vez, diferente da
estratégia pointwise do Ollama.

- `CohereReranker` — monta a requisição com todos os candidatos numa chamada só, mapeia
  `results[].relevance_score` de volta por índice.
- `CohereRerankerProviderFactory` — exige `ApiKey` (lança se em branco); usa
  `https://api.cohere.com` como `BaseUrl` padrão quando não configurado.
- `ServiceCollectionExtensions.AddCohereRerankerProvider()` — registrado em `Program.cs`
  junto com o do Ollama, então `"Cohere"` já é selecionável via `Reranking:Provider`, mas o
  `appsettings.json` padrão continua com `Provider: ""` — este provider existe para uso
  futuro, não é exercitado pela configuração padrão.

### 4. `CodeQueryResult` — campo novo

`double? RerankScore = null` adicionado como parâmetro posicional **opcional e no final**
(depois de `GitRawUrl`) em `CodeQueryResult.cs`, para não quebrar os call sites posicionais
existentes nos testes. Só é populado quando um reranker de verdade rodou (fica `null` sob
`NoOpReranker`). Espelhado em `CodeQueryResultResponse` (serializa como `rerank_score`),
`CodeQueryToolResult` (MCP), e nos métodos `ToResponse`/`ToToolResult` dos dois consumidores.

### 5. `CodeQueryService` — integração

`IReranker reranker` como 4º parâmetro do construtor primário. Dentro de `QueryAsync`:

1. `searchLimit = Math.Min(Math.Max(effectiveLimit, reranker.CandidatePoolSize),
   MaxCandidatePoolSize)` — nova constante `MaxCandidatePoolSize = 200`, independente de
   `MaxResultLimit = 50` (que só limita o `limit` vindo do chamador), como teto defensivo
   contra um `CandidatePoolSize` mal configurado.
2. Busca `searchLimit` resultados via `codeDocumentsRepository.SearchAsync` (chamada
   inalterada, só um limite potencialmente maior).
3. Chama `reranker.RerankAsync(...)` incondicionalmente — sob `NoOpReranker`,
   `CandidatePoolSize == 0`, então `searchLimit == effectiveLimit`: **zero custo extra de
   banco quando reranking está desligado** (o padrão).
4. Mapeia a ordem/notas de volta nos `CodeQueryResult` originais (`with { RerankScore =
   ... }`), trunca para `effectiveLimit`, **depois** decora com links do Git (mais barato —
   só toca o conjunto final, menor).

### 6. DI — `Program.cs`

```csharp
builder.Services.AddRerankingAbstraction(builder.Configuration);
builder.Services.AddOllamaRerankerProvider();
builder.Services.AddCohereRerankerProvider();
```
logo após o bloco de embeddings. Depois de `app.Build()`, ao lado do
`GetRequiredService<IEmbeddingGenerator>()` existente:
```csharp
app.Services.GetRequiredService<IReranker>();
```
Resolve para `NoOpReranker` sem lançar quando desligado, mas ainda pega um `Provider`
digitado errado no startup. `CodeRag.Api.csproj` ganhou `<ProjectReference>` para os três
novos projetos (`Abstraction`/`Ollama`/`Cohere`); `CodeRag.Application.csproj` ganhou
referência a `CodeRag.Reranking.Abstraction` (usada por `CodeQueryService`).

### 7. Configuração — `appsettings.json`

```json
"Reranking": {
  "Provider": "",
  "Model": "",
  "BaseUrl": "",
  "ApiKey": "",
  "TimeoutSeconds": 30,
  "CandidatePoolSize": 25,
  "MaxConcurrency": 4
}
```
`Provider: ""` mantém o comportamento de hoje idêntico (desligado). Para ligar contra uma
instância Ollama própria, via variáveis de ambiente (mesma convenção já usada por
`Embeddings__*`):
```bash
Reranking__Provider=Ollama
Reranking__Model=qwen2.5:7b-instruct
Reranking__BaseUrl=http://192.168.1.212:11434
```
(exige um modelo instruct já baixado nessa instância Ollama — `bge-m3` não serve).

### 8. `openapi.yaml`

`rerank_score` (number, nullable) adicionado ao schema `CodeQueryResultResponse`.

**⚠️ Achado durante a execução, não previsto no plano original:** diferente do que o
`.specs/code-queries-filters.md` relatou (incompatibilidade entre
`swashbuckle.aspnetcore.cli` e `Swashbuckle.AspNetCore.SwaggerGen`), desta vez a ferramenta
`10.2.3` já fixada em `.config/dotnet-tools.json` funcionou de primeira ao rodar o `.dll`
net9.0 diretamente:
```bash
dotnet ~/.nuget/packages/swashbuckle.aspnetcore.cli/10.2.3/tools/net9.0/any/dotnet-swagger.dll \
  tofile --output openapi.regenerated.yaml --yaml src/CodeRag.Api/bin/Debug/net9.0/CodeRag.Api.dll v1
```
O `diff` entre o `openapi.yaml` editado manualmente e o regenerado pela ferramenta deu
**vazio** — a edição manual bateu exatamente com a saída oficial, então não houve
necessidade de substituir o arquivo.

## Testes

- `tests/CodeRag.Reranking.Abstraction.Tests`: `RerankerResolverTests` (vazio/`"None"` →
  `NoOpReranker`, provider desconhecido → `InvalidOperationException`, provider conhecido →
  resolve a factory certa, case-insensitive) e `NoOpRerankerTests` (candidatos devolvidos na
  ordem original, sem nota).
- `tests/CodeRag.Reranking.Ollama.Tests`: reaproveita o padrão `FakeHttpMessageHandler` de
  `tests/CodeRag.Embeddings.Ollama.Tests` — nota normalizada, ordenação decrescente por
  nota, corpo da requisição contém model/pergunta/texto do candidato, erro HTTP 500 →
  `RerankingException`, resposta sem payload de nota → `RerankingException`, resposta com
  JSON malformado → `RerankingException`, `CandidatePoolSize` reflete a config. Mais
  `OllamaRerankerProviderFactoryTests` (mesmo padrão dos testes de factory de embeddings).
- `tests/CodeRag.Reranking.Cohere.Tests`: mapeamento de resultado por índice de volta para o
  id do candidato, e testes de factory (exige `ApiKey`, nome do provider, criação com sucesso).
- `tests/CodeRag.Application.Tests/CodeQueries/CodeQueryServiceTests.cs`: adicionado
  substituto (NSubstitute) de `IReranker` no construtor de todo teste existente, configurado
  como passthrough (`CandidatePoolSize` 0, `RerankAsync` devolve candidatos inalterados) —
  todas as asserções pré-existentes continuam válidas sem modificação. Testes novos:
  expansão do `searchLimit` para `CandidatePoolSize`, teto em `MaxCandidatePoolSize`,
  reordenação por `RerankScore`, truncamento para o `limit` pedido após reranking, e
  `RerankScore` fica `null` quando o reranker não pontua.

## Verificação feita

- `dotnet build` na solution inteira: **0 avisos, 0 erros** (build limpo; os warnings de
  ordering do StyleCop encontrados durante o desenvolvimento — SA1204/SA1201/SA1202 sobre a
  posição de `BuildPrompt` em `OllamaReranker` — foram resolvidos reordenando os membros:
  propriedades públicas → métodos públicos → métodos privados estáticos → métodos privados
  de instância → records aninhados).
- `dotnet test` na solution inteira: **186 testes, 0 falhas** (unit tests + integração real
  com Postgres via Testcontainers + HTTP end-to-end via `WebApplicationFactory`), incluindo
  os 10 + 10 + 4 testes novos dos três projetos de reranking e os 6 testes novos adicionados
  a `CodeQueryServiceTests`.
- `openapi.yaml` regenerado via `dotnet-swagger.dll` e comparado por `diff` contra a edição
  manual: **idêntico**.
- Smoke test manual contra uma instância Ollama real com reranking **habilitado** não foi
  feito nesta sessão (exigiria uma instância Ollama acessível com um modelo instruct já
  baixado) — a cobertura de que o fluxo funciona fim a fim vem dos testes automatizados
  (unitários com HTTP fake + testes de `CodeQueryService` cobrindo expansão do pool,
  reordenação e truncamento). Recomenda-se validar manualmente contra
  `http://192.168.1.212:11434` (mesma instância já usada por `Embeddings:BaseUrl`) com um
  modelo instruct pulled antes de habilitar em produção.
