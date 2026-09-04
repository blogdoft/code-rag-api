using Xunit;

// -m:1 already forces MSBuild to run one test PROJECT at a time (see the "Test" step comment in
// .forgejo/workflows/docker-publish.yml), but within a single project xUnit still runs different
// test classes/collections concurrently by default. Several projects here (CodeRag.Api.Tests,
// CodeRag.Infrastructure.Database.Tests, CodeRag.Embeddings.Ollama.Tests,
// CodeRag.Embeddings.OpenAI.Tests) spin up Testcontainers-managed containers - concurrent
// startups compete for the same Docker daemon and have caused flaky Ryuk resource-reaper
// timeouts (TaskCanceledException from DockerContainer.StartAsync) even on a single, otherwise
// unopposed CI run. Disabling parallelization here (applied to every test project via
// tests/Directory.Build.props) fully serializes test execution, trading run time for
// reliability on the resource-constrained self-hosted runner.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
