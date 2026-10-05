using System.Reflection;
using Dapper;

namespace CheckApps.Infrastructure.Sqlite;

/// <summary>
/// Aplica schema.sql (idempotente) na inicialização. O script é recurso embutido para
/// funcionar igual a partir da API, do seed e dos testes, sem depender de caminho relativo.
/// </summary>
public class SqliteSchemaBootstrapper(SqliteConnectionFactory connectionFactory)
{
    private const string ResourceName = "CheckApps.Infrastructure.Sqlite.schema.sql";

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        await connection.ExecuteAsync(new CommandDefinition(ReadSchema(), cancellationToken: cancellationToken));
    }

    private static string ReadSchema()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Recurso embutido '{ResourceName}' não encontrado.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
