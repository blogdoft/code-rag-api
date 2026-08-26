namespace CodeRag.Infrastructure.Database;

/// <summary>Configuration bound from the "Database" configuration section.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Npgsql connection string. This API never runs migrations against it.</summary>
    public string ConnectionString { get; set; } = string.Empty;
}
