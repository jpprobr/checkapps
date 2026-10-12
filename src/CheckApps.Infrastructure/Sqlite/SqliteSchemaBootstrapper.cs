using System.Data;
using System.Reflection;
using Dapper;

namespace CheckApps.Infrastructure.Sqlite;

/// <summary>
/// Aplica schema.sql (idempotente) na inicialização. O script é recurso embutido para
/// funcionar igual a partir da API, do seed e dos testes, sem depender de caminho relativo.
///
/// Depois do script, acrescenta colunas que bancos criados antes delas ainda não têm:
/// <c>CREATE TABLE IF NOT EXISTS</c> não altera uma tabela existente. É um paliativo aditivo e
/// idempotente — só <c>ADD COLUMN</c>, nunca recria nem converte — até existirem migrations
/// numeradas (a partir do primeiro deploy com dados reais; ver sqlite-schema.md).
/// </summary>
public class SqliteSchemaBootstrapper(SqliteConnectionFactory connectionFactory)
{
    private const string ResourceName = "CheckApps.Infrastructure.Sqlite.schema.sql";

    /// <summary>Colunas acrescentadas depois do schema original. Toda coluna nova entra aqui E no CREATE TABLE do schema.sql.</summary>
    private static readonly (string Table, string Column, string Definition)[] AdditiveColumns =
    [
        ("repositories", "sigla", "TEXT NULL"),
        ("repositories", "sigla_app", "TEXT NULL"),
        ("repositories", "role", "TEXT NULL"),
        ("repositories", "source", "TEXT NULL")
    ];

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        await connection.ExecuteAsync(new CommandDefinition(ReadSchema(), cancellationToken: cancellationToken));

        await EnsureAdditiveColumnsAsync(connection, cancellationToken);

        // Único parcial: a sigla-app identifica um repositório, mas só quando existe (repositórios
        // de infra não têm). Fica aqui, e não no schema.sql, porque depende da coluna acima.
        await connection.ExecuteAsync(new CommandDefinition(
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_repositories_sigla_app ON repositories (sigla_app) WHERE sigla_app IS NOT NULL",
            cancellationToken: cancellationToken));
    }

    private static async Task EnsureAdditiveColumnsAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        foreach (var tableGroup in AdditiveColumns.GroupBy(c => c.Table))
        {
            var existing = (await connection.QueryAsync<string>(new CommandDefinition(
                $"SELECT name FROM pragma_table_info('{tableGroup.Key}')", cancellationToken: cancellationToken)))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var (table, column, definition) in tableGroup.Where(c => !existing.Contains(c.Column)))
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    $"ALTER TABLE {table} ADD COLUMN {column} {definition}", cancellationToken: cancellationToken));
            }
        }
    }

    private static string ReadSchema()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Recurso embutido '{ResourceName}' não encontrado.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
