namespace CheckApps.Infrastructure.Sqlite;

public class SqliteOptions
{
    public const string SectionName = "Sqlite";

    /// <summary>
    /// Caminho do arquivo do banco.
    /// <para>
    /// Absoluto: usado como está — é o caso de produção (ex.: /data/checkapps.db no container).
    /// </para>
    /// <para>
    /// Relativo: resolvido contra %LOCALAPPDATA%/CheckApps (ou equivalente no SO), e NÃO contra o
    /// diretório de trabalho. É deliberado: o ASP.NET Core troca o diretório de trabalho para o do
    /// projeto no startup e o console do seed não, então depender do CWD faria API e seed abrirem
    /// bancos diferentes.
    /// </para>
    /// </summary>
    public string DatabasePath { get; set; } = "checkapps.db";

    public string ResolvePath()
    {
        if (Path.IsPathRooted(DatabasePath))
        {
            return DatabasePath;
        }

        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CheckApps");

        return Path.Combine(root, DatabasePath);
    }
}
