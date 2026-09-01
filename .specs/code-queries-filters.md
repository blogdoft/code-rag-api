# Filtros de Kind/Namespace/TypeName em `POST /api/v1/projects/{projectId}/code-queries`

**Status: implementado.** Este documento foi o plano original e foi atualizado durante a
execução para refletir o desenho final (algumas premissas do plano inicial se mostraram erradas
ou incompletas na hora de implementar — ver notas marcadas "⚠️ correção" abaixo).

## Contexto

O endpoint `POST /api/v1/projects/{projectId}/code-queries` hoje só aceita `question` no
corpo da requisição e busca por similaridade vetorial em `public.code_documents`. O pedido é
adicionar três filtros opcionais — `Kind`, `Namespace`, `TypeName` — cada um com seu próprio
conjunto de operadores (Contém/Igual/Diferente, variando por campo), sem afetar o
comportamento atual quando omitidos, e posicionar essas novas condições **primeiro** na
cláusula `WHERE` do SQL de busca. O MCP (`query_project_code`) precisa expor os mesmos
filtros, já que ele chama a camada de aplicação diretamente (sem passar pelo controller HTTP).

Levantamento feito: não existia nenhum enum de `Kind` (é `text` livre) nem um padrão de
filtro multi-operador no repo — o único precedente era o filtro `name` de `GET /projects`
(operador fixo, ILIKE, estilo `(@Param IS NULL OR ...)`). A coluna `namespace` já existia na
tabela `code_documents` mas nunca tinha sido usada (nem no SELECT, nem em nenhum DTO). `kind` é
`NOT NULL`; `namespace` e `type_name` são nullable. `embedding` também é `NOT NULL` no
schema (`db/init.sql`) e nunca é nulo em runtime — por isso nenhuma condição deste desenho faz
checagem de nulidade em `cd.embedding`; os únicos `IS NULL OR` são para `cd.namespace`/
`cd.type_name`, que são de fato nullable.

**Biblioteca `BlogDoFT.Libs.DapperUtils.Postgres`/`.Abstractions`** (mesmo autor do
`BlogDoFT.Libs.ResultPattern` já usado no projeto —
[github.com/blogdoft/BlogDoFT.Libs](https://github.com/blogdoft/BlogDoFT.Libs)) forneceu duas
peças reaproveitadas aqui:
- `WhereBuilder` (`AndWith(paramValue, condition)`/`OrWith`): builder fluente que só inclui uma
  condição no `WHERE` quando `paramValue` não é `null` — usado para montar só o trecho dos 3
  filtros novos, sem precisar do estilo `(@Param IS NULL OR ...)` embutido no texto SQL.
- `SqlExtensions.AsSqlWildCard(this string value, bool toUpperCase = true)` (em
  `BlogDoFT.Libs.DapperUtils.Abstractions.Extensions`) — troca `*` por `%`; usada com
  `toUpperCase: false` (já que a comparação usa `ILIKE`, que é case-insensitive por si só) para
  dar suporte a wildcard nos operadores `Contains`/`NotContains` (ver seção de wildcard abaixo).
  **Limitação aceita:** não escapa `%`/`_` já presentes no valor do usuário, então um valor
  literal com `%` ou `_` também age como wildcard do ILIKE — trade-off aceito para reaproveitar
  a lib em vez de escrever escaping próprio.

Não há dependência de versão em conflito: a lib usa `Npgsql 9.0.5` e `Dapper 2.1.79` (net9.0),
idênticos ao que `CodeRag.Infrastructure.Database` já referenciava.
`OrderByResolver`/`PaginatedSqlBuilder`/`PageFilter` (paginação/ordenação) e
`SqlExtensions.ToSearchable` (normalização accent-insensitive) também existem na lib mas não
foram usados: o endpoint não pagina, e não foi pedida busca insensível a acento.

**Decisões confirmadas com o usuário:**
- Cada campo tem seu **próprio enum de operador** (`KindFilterOperator`,
  `NamespaceFilterOperator`, `TypeNameFilterOperator`), restringindo no próprio schema
  (OpenAPI/MCP) as combinações operador+campo válidas — sem necessidade de validar em
  runtime "operador não permitido para este campo".
- Para `NotContains`/`NotEquals` em colunas nullable (`namespace`, `type_name`), uma linha
  com valor `NULL` **é incluída** no resultado (NULL trivialmente "não contém"/"é diferente
  de" qualquer valor).
- `MinSimilarity`, que já existia na query original com `(@MinSimilarity::float8 IS NULL OR
  ... >= @MinSimilarity)`, passou a usar um valor sentinela: quando omitido, o parâmetro
  Dapper recebe `double.MinValue` em vez de `null` (nenhuma similaridade real fica abaixo
  disso, então a comparação `>=` vira um no-op) — elimina o `OR`/`IS NULL` desnecessário no
  SQL. A assinatura pública (`double? minSimilarity`) continua igual; só a resolução do
  parâmetro Dapper aplica o default.
- **Wildcard com `*`:** nos operadores `Contains`/`NotContains` (que usam `ILIKE`), o valor do
  usuário **não é** mais automaticamente envolvido em `'%' || valor || '%'`. Em vez disso, `*`
  no valor vira `%` (via `AsSqlWildCard`), e o valor é usado como o padrão `ILIKE` literal. Sem
  `*`, o resultado é uma comparação exata (case-insensitive) — não mais um "contém" implícito.
  Ex.: `"fun*"` = começa com "fun"; `"*fun*"` = contém "fun" (equivalente ao comportamento
  antigo); `"fun"` sem `*` = igual exato a "fun", case-insensitive. `Equals`/`NotEquals`
  continuam comparação exata via `=`/`<>`, sem interpretar `*`.

## Desenho do contrato

Corpo da requisição (JSON, convenção snake_case já usada no projeto):

```json
{
  "question": "...",
  "kind": { "operator": "equals", "value": "function" },
  "namespace": { "operator": "contains", "value": "*Foo.Bar*" },
  "type_name": { "operator": "not_contains", "value": "Controller*" }
}
```

Os três filtros são objetos opcionais `{operator, value}` — se omitidos, comportamento atual
preservado; se presentes, tanto `operator` quanto `value` são obrigatórios.

**⚠️ Correção em relação ao plano original:** a premissa inicial era que records posicionais
com System.Text.Json tornam os parâmetros do construtor obrigatórios por padrão (um filtro sem
`value` resultaria em 400 automático). Isso foi **testado isoladamente e confirmado falso**:
por padrão, um campo ausente vira `default` silenciosamente (string vazia / primeiro valor do
enum), sem erro. A correção foi anotar `[property: JsonRequired]` em `Operator` e `Value` nos
3 records de filtro (`CodeQueryKindFilterRequest`, etc.), o que faz o STJ lançar `JsonException`
(→ 400 automático) quando um dos dois campos falta — comportamento verificado com teste
dedicado antes de aplicar, e coberto pelos testes de integração HTTP.

Como nenhum enum era usado em nenhum contrato antes, foi preciso registrar um
`JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)` em `Program.cs` (dentro do
`AddJsonOptions` existente) para os valores saírem como
`"contains" | "not_contains" | "equals" | "not_equals"` em vez de inteiros.

## Mudanças por camada

### 1. `CodeRag.Application/CodeQueries` (domínio)

- **3 enums**, um arquivo cada: `KindFilterOperator.cs` (`Contains, Equals, NotEquals`),
  `NamespaceFilterOperator.cs` (`Contains, NotContains, Equals, NotEquals`),
  `TypeNameFilterOperator.cs` (`Contains, NotContains, Equals`).
- **`CodeQueryFailures.cs`**: 6 failures novas seguindo o padrão exato já usado (prefixo de
  status HTTP no `Code`), uma dupla `*ValueRequired`/`*ValueTooLong(maxLength)` por campo.
- **`ICodeQueryService` / `CodeQueryService`**: 6 parâmetros opcionais a mais em `QueryAsync`
  — `kindOperator`/`kindValue`, `namespaceOperator`/`namespaceValue`,
  `typeNameOperator`/`typeNameValue` — como escalares soltos, no mesmo estilo de
  `limit`/`minSimilarity` já existentes. Validação em `CodeQueryService.QueryAsync`, antes da
  busca do projeto, reaproveitando o padrão de `ProjectsService.ListAsync`: para cada campo,
  se o operador foi informado mas o valor é vazio/whitespace → `*ValueRequired`; se passar de
  `MaxFilterValueLength = 200` (mesma constante de `ProjectsService.MaxNameFilterLength`) →
  `*ValueTooLong`. Repassa os 6 parâmetros para `codeDocumentsRepository.SearchAsync`.

### 2. `CodeRag.Application/CodeQueries/ICodeDocumentsRepository.cs` + `CodeRag.Infrastructure.Database/CodeQueries/CodeDocumentsRepository.cs`

- `<PackageReference Include="BlogDoFT.Libs.DapperUtils.Postgres" Version="1.12.0" />` em
  `CodeRag.Infrastructure.Database.csproj` (referência direta, sem central package
  management no repo). Traz `BlogDoFT.Libs.DapperUtils.Abstractions` como dependência
  transitiva (de onde vem `AsSqlWildCard`) — não precisa referenciar à parte.
- Interface: mesmos 6 parâmetros novos em `SearchAsync`.
- Cada filtro tem uma função `XCondition(operador)` que devolve o trecho SQL certo — sem
  branches `@Operator = 'X' AND ...` dentro do SQL:

```csharp
private static string KindCondition(KindFilterOperator kindOperator) => kindOperator switch
{
    KindFilterOperator.Contains => "cd.kind ILIKE @KindValue",
    KindFilterOperator.Equals => "cd.kind = @KindValue",
    KindFilterOperator.NotEquals => "cd.kind <> @KindValue",
    _ => throw new ArgumentOutOfRangeException(nameof(kindOperator), kindOperator, null),
};

private static string NamespaceCondition(NamespaceFilterOperator namespaceOperator) => namespaceOperator switch
{
    NamespaceFilterOperator.Contains => "cd.namespace ILIKE @NamespaceValue",
    NamespaceFilterOperator.NotContains => "(cd.namespace IS NULL OR cd.namespace NOT ILIKE @NamespaceValue)",
    NamespaceFilterOperator.Equals => "cd.namespace = @NamespaceValue",
    NamespaceFilterOperator.NotEquals => "(cd.namespace IS NULL OR cd.namespace <> @NamespaceValue)",
    _ => throw new ArgumentOutOfRangeException(nameof(namespaceOperator), namespaceOperator, null),
};

private static string TypeNameCondition(TypeNameFilterOperator typeNameOperator) => typeNameOperator switch
{
    TypeNameFilterOperator.Contains => "cd.type_name ILIKE @TypeNameValue",
    TypeNameFilterOperator.NotContains => "(cd.type_name IS NULL OR cd.type_name NOT ILIKE @TypeNameValue)",
    TypeNameFilterOperator.Equals => "cd.type_name = @TypeNameValue",
    _ => throw new ArgumentOutOfRangeException(nameof(typeNameOperator), typeNameOperator, null),
};
```

  (Nenhuma condição envolve o valor em `'%' || ... || '%'` mais — ver seção de wildcard. O
  `IS NULL OR` embutido em `NotContains`/`NotEquals` de `namespace`/`type_name` é o que aplica
  a semântica combinada com o usuário: linha com coluna `NULL` entra no resultado.)

- **⚠️ Correção em relação ao plano original:** o desenho inicial tinha as funções `XCondition`
  aceitando `KindFilterOperator?` (nullable) e eram passadas como argumento posicional direto
  a `WhereBuilder.AndWith(valor, XCondition(operador))`. Isso tem um bug real: argumentos de
  método em C# são avaliados antes da chamada, então `XCondition(operador)` seria executado
  **mesmo quando nenhum filtro foi passado** (operador `null`), caindo sempre no branch
  `_ => throw`. Ou seja, toda consulta sem filtro novo lançaria exceção. A correção: as funções
  `XCondition` passaram a receber o operador **não-nulo**, e a chamada só acontece dentro de um
  `if (operador is not null && valor is not null)` — assim `AndWith`/`XCondition` só são
  chamados quando o filtro está de fato ativo:

```csharp
var where = new WhereBuilder();
if (kindOperator is not null && kindValue is not null)
{
    where.AndWith(kindValue, KindCondition(kindOperator.Value));
}
if (namespaceOperator is not null && namespaceValue is not null)
{
    where.AndWith(namespaceValue, NamespaceCondition(namespaceOperator.Value));
}
if (typeNameOperator is not null && typeNameValue is not null)
{
    where.AndWith(typeNameValue, TypeNameCondition(typeNameOperator.Value));
}

var newFilters = where.Build().ToString();

// Build() devolve "" quando nenhum filtro foi passado, ou "where (cond1)  and (cond2) ...".
// Troca-se o "where " pelo prefixo que entra antes das condições fixas existentes.
var newFiltersPrefix = newFilters.Length > 0
    ? newFilters["where ".Length..] + " and "
    : string.Empty;

var sql = $"""
    SELECT cd.id AS Id
         , cd.source_file AS SourceFile
         , cd.kind AS Kind
         , cd.type_name AS TypeName
         , cd.member AS Member
         , cd.embedding_text AS EmbeddingText
         , ROUND((1 - (cd.embedding <=> @Embedding))::numeric, 10)::float8 AS Similarity
    FROM public.code_documents cd
    JOIN public.embedding_models em ON em.id = cd.embedding_model_id
    WHERE {newFiltersPrefix}cd.project_id = @ProjectId
      AND em.provider = @EmbeddingProvider
      AND em.model = @EmbeddingModel
      AND em.dimensions = @EmbeddingDimensions
      AND (1 - (cd.embedding <=> @Embedding)) >= @MinSimilarity
    ORDER BY cd.embedding <=> @Embedding
    LIMIT @Limit
    """;

var parameters = new
{
    ProjectId = projectId,
    EmbeddingProvider = embeddingProvider,
    EmbeddingModel = embeddingModel,
    EmbeddingDimensions = embeddingDimensions,
    Embedding = new Vector(queryEmbedding.ToArray()),
    Limit = limit,
    MinSimilarity = minSimilarity ?? double.MinValue,
    KindValue = kindOperator is KindFilterOperator.Contains ? kindValue?.AsSqlWildCard(toUpperCase: false) : kindValue,
    NamespaceValue = namespaceOperator is NamespaceFilterOperator.Contains or NamespaceFilterOperator.NotContains
        ? namespaceValue?.AsSqlWildCard(toUpperCase: false)
        : namespaceValue,
    TypeNameValue = typeNameOperator is TypeNameFilterOperator.Contains or TypeNameFilterOperator.NotContains
        ? typeNameValue?.AsSqlWildCard(toUpperCase: false)
        : typeNameValue,
};
```

  Quando os 3 filtros são omitidos, `newFiltersPrefix` é `""` e o SQL gerado é **idêntico** ao
  original — preserva o comportamento existente. Quando presentes, o trecho novo cai
  literalmente antes de `cd.project_id = ...`, satisfazendo "os filtros novos vêm primeiro no
  WHERE". Continua 100% parametrizado (nomes de coluna e keywords são texto fixo no
  código-fonte, só os valores variam via `@KindValue`/`@NamespaceValue`/`@TypeNameValue`) —
  sem risco de injection, mesmo trocando `const string sql` por uma string montada em runtime.
- `SELECT` não inclui `cd.namespace` — o filtro é só no `WHERE`; `namespace` não foi exposto na
  resposta (não foi pedido, mantém escopo enxuto).

### 3. `CodeRag.Api` (REST)

- 3 records em `Contracts/`: `CodeQueryKindFilterRequest.cs`,
  `CodeQueryNamespaceFilterRequest.cs`, `CodeQueryTypeNameFilterRequest.cs` — cada um um
  record `(TOperator Operator, string Value)` com `[property: JsonRequired]` nos dois campos,
  referenciando o enum de `CodeRag.Application.CodeQueries` diretamente.
- `CodeQueryRequest.cs`: `Kind`, `Namespace`, `TypeName` opcionais (default `null`). Mantém
  `[JsonUnmappedMemberHandling(Disallow)]`.
- `CodeQueriesController.QueryAsync`: repassa os 3 filtros ao `codeQueryService.QueryAsync`
  via `request.Kind?.Operator`, `request.Kind?.Value`, etc. Não mexe no gap pré-existente de
  `limit`/`minSimilarity` não estarem no contrato HTTP — fora do escopo pedido.
- `Program.cs`: `JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)` no `AddJsonOptions`.
- Doc XML (`<summary>`/`<param>`) nos novos records/parâmetros usa `<c>...</c>` para os nomes
  de operador (não `<see cref>` — Swashbuckle renderiza `<see cref>` como o nome totalmente
  qualificado do tipo/membro na descrição do OpenAPI, o que fica feio e não é o padrão já
  usado pelos outros contratos do projeto).

### 4. `CodeRag.Mcp/Tools/CodeQueryTools.cs`

- 6 parâmetros opcionais a mais em `QueryProjectCodeAsync`, tipados diretamente com os enums
  da camada de aplicação, cada um com `[Description]` documentando o campo, os operadores
  válidos e o comportamento do wildcard `*`. O SDK de MCP reflete o enum no JSON schema da
  tool automaticamente. Repassa direto para `codeQueryService.QueryAsync(...)`. Validação de
  domínio (value obrigatório, tamanho máximo) já vem de graça pelo `Result<T>` existente —
  falha vira `McpException` no mesmo `onFailure` que já existia.

### 5. Ferramenta de geração do `openapi.yaml`

**⚠️ Achado durante a execução, não previsto no plano original:** o `swashbuckle.aspnetcore.cli`
fixado em `.config/dotnet-tools.json` (9.0.6) já estava incompatível com o
`Swashbuckle.AspNetCore.SwaggerGen` 10.2.3 usado pela API (`TypeLoadException` ao rodar) — uma
quebra pré-existente, não causada por este trabalho. Corrigido subindo o tool para `10.2.3` no
`dotnet-tools.json`. A regeneração completa também corrigiu uma divergência pré-existente entre
a doc XML de `GitRawUrl`/`GitUrl` (`CodeQueryResultResponse.cs`) e o `openapi.yaml` commitado,
que já estava desatualizado antes deste trabalho. O `dotnet tool run swagger` também falhou
separadamente (tentava resolver um build net10.0 incompatível com o ambiente) — funcionou
rodando o `.dll` net9.0 da ferramenta diretamente:
`dotnet ~/.nuget/packages/swashbuckle.aspnetcore.cli/10.2.3/tools/net9.0/any/dotnet-swagger.dll tofile --output openapi.yaml --yaml <caminho-do-CodeRag.Api.dll> v1`

### 6. Testes

- `CodeQueryServiceTests.cs`: passthrough dos 6 parâmetros novos para o repositório (mock
  NSubstitute), validação de valor obrigatório quando operador setado, validação de valor
  longo demais.
- `CodeDocumentsRepositoryTests.cs` (Testcontainers, Postgres real): um caso por operador de
  cada campo, casos específicos confirmando que linha com coluna `NULL` aparece no resultado
  para `NotContains`/`NotEquals`, e 3 casos de wildcard (`"fun"` sem `*` → match exato,
  `"Shop.*"` → prefixo, `"*Controller"` → sufixo).
- `CodeQueriesEndpointTests.cs`: filtro sem `value`/`operator` → 400 (JsonRequired); operador
  inválido para o enum do campo → 400; `value` vazio → 400 (falha de domínio); `value` longo
  demais → 400. A fixture (`CustomWebApplicationFactory`) aponta o provedor de embeddings para
  um endereço não roteável de propósito (para testar o path de 500), então **não é possível
  obter um 200 real** nesta suíte — a cobertura de "filtros omitidos preservam o comportamento
  atual" já existe implicitamente no teste pré-existente que espera 500 nesse cenário.
- `CodeQueryToolsTests.cs`: passthrough dos novos parâmetros da tool MCP para
  `ICodeQueryService.QueryAsync`.
- `Schema.cs` (em `CodeRag.Api.Tests` e `CodeRag.Infrastructure.Database.Tests`) já tinha as
  colunas `namespace`/`type_name`/`kind` — nenhuma mudança necessária.

## Verificação feita

- `dotnet test` no solution inteiro: **157 testes, 0 falhas** (unit + integration real com
  Postgres via Testcontainers + HTTP end-to-end via `WebApplicationFactory`).
- Build local limpo (só warnings pré-existentes e não relacionados, de um bug conhecido do
  StyleCop analyzer em outros projetos do repo).
- `openapi.yaml` regenerado e validado como YAML válido.
- Smoke test via MCP **não foi feito**: o servidor MCP conectado nesta sessão roda um build
  anterior ao deste trabalho (sem os novos parâmetros) — validar exigiria reiniciar o servidor
  local a partir do código atualizado.
