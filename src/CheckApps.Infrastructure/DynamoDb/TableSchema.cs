namespace CheckApps.Infrastructure.DynamoDb;

/// <summary>
/// Nomes de tabela, atributos de chave e índices da tabela única do CheckApps.
/// Ver seção 4 (DynamoDB Design) do plano para o racional de cada GSI.
/// </summary>
public static class TableSchema
{
    public const string DefaultTableName = "CheckApps";

    public const string Pk = "PK";
    public const string Sk = "SK";

    public const string Gsi1Name = "GSI1";
    public const string Gsi1Pk = "GSI1PK";
    public const string Gsi1Sk = "GSI1SK";

    public const string Gsi2Name = "GSI2";
    public const string Gsi2Pk = "GSI2PK";
    public const string Gsi2Sk = "GSI2SK";
}
