using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace CheckApps.Infrastructure.Sqlite;

/// <summary>
/// Abre conexões já configuradas. Os PRAGMAs são por conexão, não por banco — em
/// especial foreign_keys, que o SQLite deixa DESLIGADO por padrão: sem ele as
/// cascatas declaradas no schema simplesmente não acontecem.
/// </summary>
public class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(IOptions<SqliteOptions> options)
    {
        var path = options.Value.ResolvePath();

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public IDbConnection Create()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var pragmas = connection.CreateCommand();
        pragmas.CommandText = """
            PRAGMA foreign_keys = ON;
            PRAGMA journal_mode = WAL;
            PRAGMA busy_timeout = 5000;
            """;
        pragmas.ExecuteNonQuery();

        return connection;
    }
}
