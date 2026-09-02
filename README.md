# Code RAG API

API for managing indexed code projects and querying their vectorized source code using
natural language. Implements the contract in [`openapi.yaml`](./openapi.yaml): the service
stores per-project code documents (functions, types, members, etc.) along with vector
embeddings in Postgres/pgvector, and retrieves the most semantically similar pieces of code
for a natural-language question. It also exposes the same functionality over **MCP** for
LLM clients doing code research.

## Architecture

| Layer | Project | Responsibility |
|---|---|---|
| API | `CodeRag.Api` | ASP.NET Core startup, controllers, RFC 7807 error formatting |
| Application | `CodeRag.Application` | Business logic (`ProjectsService`, `CodeQueryService`), domain validation |
| Embeddings | `CodeRag.Embeddings.Abstraction/.Local/.Ollama/.OpenAI` | Provider-agnostic embedding generation, selected at runtime by config |
| Reranking | `CodeRag.Reranking.Abstraction/.Ollama/.Cohere` | Optional reranking of vector-search candidates, selected at runtime by config; disabled by default |
| Infrastructure | `CodeRag.Infrastructure.Database` | Dapper/Npgsql/pgvector repositories |
| MCP | `CodeRag.Mcp` | MCP tools wrapping the Application layer for LLM clients |

The API never runs migrations — `db/init.sql` exists only to seed the schema for local/docker use.

## Running locally

```bash
docker compose up -d
```

This starts Postgres (pgvector, schema pre-seeded), Ollama, and the API on `http://localhost:8080`.
Pull an embedding model into Ollama once it's up:

```bash
docker compose exec ollama ollama pull bge-m3
```

To run the API outside Docker, point it at your own Postgres/embedding provider via
`src/CodeRag.Api/appsettings.json` (or environment variables, e.g. `ConnectionStrings__Database`,
`Embeddings__Provider`) and:

```bash
dotnet run --project src/CodeRag.Api
```

## Configuration

- `ConnectionStrings:Database` — Npgsql connection string.
- `Embeddings:Provider` — `Local`, `Ollama`, or `OpenAI`.
- `Embeddings:Model`, `Embeddings:Dimensions`, `Embeddings:Normalized` — must match a row in `embedding_models`.
- `Embeddings:BaseUrl` / `Embeddings:ApiKey` — used by the Ollama/OpenAI providers.
- `Embeddings:LocalModelPath` — directory containing `model.onnx` + `vocab.txt`, used by the Local provider.
- `Reranking:Provider` — empty (default) or `None` disables reranking; `Ollama` or `Cohere` enables it.
- `Reranking:Model`, `Reranking:BaseUrl` / `Reranking:ApiKey` — used by the Ollama/Cohere providers.
- `Reranking:CandidatePoolSize` — how many top vector-search results to rerank before truncating to the caller's requested limit (default 25).
- `Reranking:MaxConcurrency` — max concurrent scoring calls issued to the provider (Ollama's pointwise strategy; default 4).

## Enabling reranking with Ollama

Reranking is disabled by default (`Reranking:Provider` empty). To turn it on against an
Ollama instance, pull a **chat/instruct** model — not an embedding model. The embedding
model already configured under `Embeddings:Model` (e.g. `bge-m3`) cannot be reused here:
`CodeRag.Reranking.Ollama` scores candidates by prompting the model for a relevance grade,
which requires instruction-following, something embedding-only models don't support.

```bash
# against the docker-compose Ollama service
docker compose exec ollama ollama pull qwen2.5:7b-instruct

# against your own Ollama host
ollama pull qwen2.5:7b-instruct
```

Any reasonably small instruct model works — pick one that fits your hardware and is
already available in your Ollama registry (`qwen2.5:7b-instruct`, `llama3.1:8b-instruct`,
`phi4`, etc.). Larger models score more accurately but add latency per query, since one
candidate is scored per call.

Then point the app at it, either in `appsettings.json` or via environment variables:

```bash
Reranking__Provider=Ollama
Reranking__Model=qwen2.5:7b-instruct
Reranking__BaseUrl=http://localhost:11434
dotnet run --project src/CodeRag.Api
```

With reranking enabled, each `code-queries`/`query_project_code` call fetches
`Reranking:CandidatePoolSize` candidates from the vector search (instead of just the
requested `limit`), scores every one of them against the question, and returns the
top-`limit` by score — each result's `rerank_score` field reflects the grade it received
(`0.0`-`1.0`), and is `null` whenever reranking is disabled. Lower `Reranking:MaxConcurrency`
if your Ollama host can't keep up with parallel scoring requests.

## REST API

- `GET /api/v1/projects?name=` — list/search indexed projects.
- `POST /api/v1/projects` — create a project.
- `GET /api/v1/projects/{projectId}` — get a project by id.
- `PUT /api/v1/projects/{projectId}` — rename a project.
- `DELETE /api/v1/projects/{projectId}` — delete a project (fails if it still has indexed code documents).
- `POST /api/v1/projects/{projectId}/code-queries` — natural-language search over a project's indexed code.

## MCP

The same functionality is exposed as MCP tools at `http://localhost:8080/mcp` (Streamable HTTP,
stateless — no session handshake required):

- **`list_projects`** — optional `name` filter, mirrors `GET /projects`.
- **`query_project_code`** — `projectId` + `question`, mirrors `POST /code-queries`.

### Configuring the MCP server

#### Claude Code

```bash
claude mcp add --transport http code-rag http://localhost:8080/mcp
```

Use `--scope user` instead of the default `--scope local` to make it available across every
project on your machine, or `--scope project` to commit a shared `.mcp.json` for the team:

```json
{
  "mcpServers": {
    "code-rag": {
      "type": "http",
      "url": "http://localhost:8080/mcp"
    }
  }
}
```

#### Codex

Codex CLI supports Streamable HTTP servers directly (no stdio bridge needed). Either run:

```bash
codex mcp add code-rag --url http://localhost:8080/mcp
```

or add it to `~/.codex/config.toml`:

```toml
[mcp_servers.code_rag]
url = "http://localhost:8080/mcp"
```

## Testing

```bash
dotnet test CodeRag.sln
```

Application and embedding-provider tests are pure unit tests (NSubstitute/Bogus/Shouldly).
`CodeRag.Infrastructure.Database.Tests` and `CodeRag.Api.Tests` are integration tests that spin
up a disposable Postgres/pgvector container via Testcontainers — a running Docker daemon is
required to run those two projects.

## CI/CD

`.github/workflows/docker-publish.yml` and `.forgejo/workflows/docker-publish.yml` both build,
test, and publish the Docker image on push to `main` and on version tags (adjust the registry
variables/secrets in the Forgejo workflow to match your instance).

On every push to `main`, the Forgejo workflow also stamps the freshly published image tag into
the manifests under [`.eng/k8s`](./.eng/k8s) and syncs them into `manifests/code-rag-api/` on the
`argo-local-apps` app-of-apps repo, which ArgoCD watches for the local k8s cluster. Requires an
`ARGO_DEPLOY_SSH_KEY` repo secret (a write-access deploy key on `argo-local-apps`), set up as
follows:

1. Generate a dedicated ed25519 keypair (no passphrase — CI can't type one):

   ```bash
   ssh-keygen -t ed25519 -N "" -C "code-rag-ci@argo-local-apps" -f argo_deploy_key
   ```

2. In `sauron/argo-local-apps` on Forgejo, go to **Settings → Deploy Keys → Add Deploy Key**,
   paste the contents of `argo_deploy_key.pub`, and check **"Allow Write Access"** (deploy keys
   are read-only by default; without write access the workflow's `git push` fails).
3. In `code-rag` (this repo) on Forgejo, go to **Settings → Actions → Secrets → Add Secret**,
   name it `ARGO_DEPLOY_SSH_KEY`, and paste the contents of `argo_deploy_key` (the private key,
   including the `BEGIN`/`END` lines).
4. Delete both local key files (`argo_deploy_key`, `argo_deploy_key.pub`) — only the copies in
   Forgejo are needed.

The Deployment also expects a `code-rag-secrets` Secret in the target namespace/cluster with a
`connection-string` key (the Npgsql connection string) — it isn't managed in this repo and must
be created once, out of band, before the app can start.
