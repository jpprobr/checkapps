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

    [Fact]
    public async Task EnsureSchema_OnADatabaseBeforeTheAnalysisTables_CreatesThem_KeepsTheData_AndIsIdempotent()
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
                    name TEXT NOT NULL, provider TEXT NULL, url TEXT NULL, external_id TEXT NULL, status TEXT NULL,
                    sigla TEXT NULL, sigla_app TEXT NULL, role TEXT NULL, source TEXT NULL);
                INSERT INTO squads (id, name) VALUES ('sq-1', 'Squad');
                INSERT INTO apps (id, squad_id, name) VALUES ('app-1', 'sq-1', 'App');
                INSERT INTO repositories (id, app_id, name, status) VALUES ('r-1', 'app-1', 'repo-existente', 'active');
                """);
        }

        var bootstrapper = new SqliteSchemaBootstrapper(_factory);
        await bootstrapper.EnsureSchemaAsync();
        await bootstrapper.EnsureSchemaAsync(); // idempotente

        using var check = _factory.Create();
        foreach (var table in new[] { "repository_analyses", "repository_dependencies", "repository_integrations", "host_links", "host_externals" })
        {
            Assert.Equal(1, check.ExecuteScalar<int>(
                $"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '{table}'"));
        }

        foreach (var index in new[]
        {
            "ix_repository_analyses_repo_analyzed", "ix_repository_dependencies_analysis", "ix_repository_dependencies_name",
            "ix_repository_integrations_analysis", "ix_repository_integrations_host", "ix_repository_integrations_reference",
            "ix_host_links_app"
        })
        {
            Assert.Equal(1, check.ExecuteScalar<int>(
                $"SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = '{index}'"));
        }

        Assert.Equal("repo-existente", check.ExecuteScalar<string>("SELECT name FROM repositories WHERE id = 'r-1'"));

        // As tabelas novas funcionam sobre o banco antigo, inclusive a cascata a partir do repositório.
        check.Execute(
            """
            INSERT INTO repository_analyses (id, repository_id, analyzed_at, status, source)
            VALUES ('an-1', 'r-1', '2026-10-24T12:00:00.0000000+00:00', 'ok', 'GITHUB_SCAN');
            INSERT INTO repository_dependencies
                (id, analysis_id, ecosystem, kind, name, project_path, source_file, origin, confidence, scanner_id, scanner_version)
            VALUES ('f-1', 'an-1', 'nuget', 'package', 'Serilog', 'App.csproj', 'App.csproj', 'PARSER', 'alta', 'dotnet', '1.0.0');
            INSERT INTO repository_integrations
                (id, analysis_id, mechanism, host, reference, config_key, state, project_path, source_file, origin, confidence, scanner_id, scanner_version)
            VALUES ('i-1', 'an-1', 'config', 'a.exemplo.com', 'ISvc', 'ISvc:BaseUrl', 'resolvida', 'App', 'App/appsettings.json', 'PARSER', 'alta', 'dotnet', '1.1.0');
            INSERT INTO host_links (id, host_key, app_id, state, origin, decided_at)
            VALUES ('l-1', 'a.{env}.exemplo.com', 'app-1', 'confirmado', 'HUMAN', '2026-11-02T12:00:00.0000000+00:00');
            DELETE FROM repositories WHERE id = 'r-1';
            """);
        Assert.Equal(0, check.ExecuteScalar<int>("SELECT COUNT(*) FROM repository_analyses"));
        Assert.Equal(0, check.ExecuteScalar<int>("SELECT COUNT(*) FROM repository_dependencies"));
        Assert.Equal(0, check.ExecuteScalar<int>("SELECT COUNT(*) FROM repository_integrations"));

        // O vínculo host→app (spec 017) não depende das análises: sobrevive a elas e só morre com o app.
        Assert.Equal(1, check.ExecuteScalar<int>("SELECT COUNT(*) FROM host_links"));
        check.Execute("DELETE FROM apps WHERE id = 'app-1'");
        Assert.Equal(0, check.ExecuteScalar<int>("SELECT COUNT(*) FROM host_links"));

        // O host externo (spec 019) também é decisão humana, fora das análises: um por host.
        check.Execute(
            """
            INSERT INTO host_externals (id, host_key, name, origin, decided_at)
            VALUES ('x-1', 'api.outra.com', 'Terceiro', 'HUMAN', '2026-11-03T12:00:00.0000000+00:00');
            """);
        Assert.Throws<SqliteException>(() => check.Execute(
            """
            INSERT INTO host_externals (id, host_key, origin, decided_at)
            VALUES ('x-2', 'api.outra.com', 'HUMAN', '2026-11-03T12:00:00.0000000+00:00');
            """));
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
