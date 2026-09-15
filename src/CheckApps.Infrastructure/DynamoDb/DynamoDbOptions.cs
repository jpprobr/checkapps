namespace CheckApps.Infrastructure.DynamoDb;

public class DynamoDbOptions
{
    public const string SectionName = "DynamoDb";

    public string TableName { get; set; } = TableSchema.DefaultTableName;

    /// <summary>
    /// Endpoint do DynamoDB Local (ex.: http://localhost:8000). Deixe nulo/vazio para
    /// usar a cadeia padrão de credenciais/região da AWS SDK contra a conta AWS real.
    /// </summary>
    public string? ServiceUrl { get; set; }

    public string Region { get; set; } = "us-east-1";
}
