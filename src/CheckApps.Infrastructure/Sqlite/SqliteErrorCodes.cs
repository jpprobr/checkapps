namespace CheckApps.Infrastructure.Sqlite;

/// <summary>
/// Códigos estendidos do SQLite. Distinguir PK/UNIQUE de FOREIGN KEY importa: só o
/// primeiro significa "esse id já existe" (e justifica novo sufixo de slug); violação
/// de FK é erro de verdade e deve propagar.
/// </summary>
internal static class SqliteErrorCodes
{
    public const int ConstraintPrimaryKey = 1555;
    public const int ConstraintUnique = 2067;
}
