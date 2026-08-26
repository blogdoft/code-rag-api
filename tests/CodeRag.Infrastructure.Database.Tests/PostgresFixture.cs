using Npgsql;
using Testcontainers.PostgreSql;

namespace CodeRag.Infrastructure.Database.Tests;

/// <summary>Spins up a disposable pgvector-enabled Postgres container shared by every test in <see cref="PostgresCollection"/>.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg16")
        .WithDatabase("coderag_test")
        .WithUsername("coderag_test")
        .WithPassword("coderag_test")
        .Build();

    public NpgsqlDataSource DataSource { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // The "vector" extension/type must exist before a vector-aware NpgsqlDataSource opens
        // its first connection - Npgsql caches the database's type catalog at that point, and
        // never sees "vector" if it was created afterwards on the same data source.
        await using (var setupConnection = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await setupConnection.OpenAsync();
            await using var command = setupConnection.CreateCommand();
            command.CommandText = Schema.Ddl;
            await command.ExecuteNonQueryAsync();
        }

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(_container.GetConnectionString());
        dataSourceBuilder.UseVector();
        DataSource = dataSourceBuilder.Build();
    }

    public async Task DisposeAsync()
    {
        await DataSource.DisposeAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
