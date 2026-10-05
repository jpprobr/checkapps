using CheckApps.Infrastructure.Sqlite;
using Microsoft.Extensions.Options;

namespace CheckApps.IntegrationTests.Sqlite;

/// <summary>
/// Banco SQLite em arquivo temporário, um por classe de teste. Sem Docker e sem estado
/// compartilhado — o que torna desnecessária a collection fixture que o DynamoDB Local
/// exigia (duas classes recriando a mesma tabela em paralelo se derrubavam).
/// </summary>
public class SqliteFixture : IAsyncLifetime
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), "checkapps-tests", $"{Guid.NewGuid():N}.db");

    public SqliteConnectionFactory ConnectionFactory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        ConnectionFactory = new SqliteConnectionFactory(
            Options.Create(new SqliteOptions { DatabasePath = _databasePath }));

        await new SqliteSchemaBootstrapper(ConnectionFactory).EnsureSchemaAsync();
    }

    public Task DisposeAsync()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        foreach (var file in new[] { _databasePath, $"{_databasePath}-wal", $"{_databasePath}-shm" })
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // Arquivo temporário preso pelo SO não deve falhar a suíte.
            }
        }

        return Task.CompletedTask;
    }
}
