using CheckApps.Infrastructure.Sqlite;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace CheckApps.IntegrationTests.Sqlite;

/// <summary>
/// O bootstrap precisa funcionar sobre um banco criado ANTES das colunas da spec 005 — o banco
/// local de quem já usava o CheckApps. CREATE TABLE IF NOT EXISTS sozinho não acrescentaria nada.
/// </summary>
public class SchemaEvolutionTests : IDisposable
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), "checkapps-tests", $"evolution-{Guid.NewGuid():N}.db");

    private readonly SqliteConnectionFactory _factory;

    public SchemaEvolutionTests()
    {
        _factory = new SqliteConnectionFactory(Options.Create(new SqliteOptions { DatabasePath = _databasePath }));
    }

    [Fact]
    public async Task EnsureSchema_OnAnOldDatabase_AddsTheColumns_KeepsTheData_AndIsIdempotent()
    {
        using (var connection = _factory.Create())
        {
            connection.Execute(
                """
                CREATE TABLE squads (id TEXT PRIMARY KEY, name TEXT NOT NULL, description TEXT NULL, status TEXT NULL);
                CREATE TABLE apps (
                    id TEXT PRIMARY KEY, squad_id TEXT NOT NULL REFERENCES squads(id), name TEXT NOT NULL,
                    description TEXT NULL, criticality TEXT NULL, status TEXT NULL, external_id TEXT NULL);
                CREATE TABLE repositories (
                    id TEXT PRIMARY KEY, app_id TEXT NOT NULL REFERENCES apps(id) ON DELETE CASCADE,
                    name TEXT NOT NULL, provider TEXT NULL, url TEXT NULL, external_id TEXT NULL, status TEXT NULL);
                INSERT INTO squads (id, name) VALUES ('sq-1', 'Squad');
                INSERT INTO apps (id, squad_id, name) VALUES ('app-1', 'sq-1', 'App');
                INSERT INTO repositories (id, app_id, name, provider, external_id, status)
                VALUES ('r-1', 'app-1', 'repo-antigo', 'github', '42', 'active');
                """);
        }

        var bootstrapper = new SqliteSchemaBootstrapper(_factory);
        await bootstrapper.EnsureSchemaAsync();
        await bootstrapper.EnsureSchemaAsync(); // idempotente

        using var check = _factory.Create();
        var columns = check.Query<string>("SELECT name FROM pragma_table_info('repositories')").ToList();
        Assert.Contains("sigla", columns);
        Assert.Contains("sigla_app", columns);
        Assert.Contains("role", columns);
        Assert.Contains("source", columns);

        var old = check.QuerySingle<(string Name, string Status, string? Sigla)>(
            "SELECT name, status, sigla FROM repositories WHERE id = 'r-1'");
        Assert.Equal("repo-antigo", old.Name);
        Assert.Equal("active", old.Status);
        Assert.Null(old.Sigla);

        Assert.Equal(1, check.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'discovered_repositories'"));
        Assert.Equal(1, check.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'ux_repositories_sigla_app'"));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
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
    }
}
