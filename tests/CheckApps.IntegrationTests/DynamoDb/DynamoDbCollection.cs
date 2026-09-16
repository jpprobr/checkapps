namespace CheckApps.IntegrationTests.DynamoDb;

/// <summary>
/// Todas as classes de teste que usam DynamoDbLocalFixture precisam compartilhar UMA
/// única instância (via [Collection]) em vez de IClassFixture por classe — senão o
/// xUnit roda as classes em paralelo e cada fixture apaga/recria a mesma tabela
/// física, derrubando os testes das outras classes no meio da execução.
/// </summary>
[CollectionDefinition(Name)]
public class DynamoDbCollection : ICollectionFixture<DynamoDbLocalFixture>
{
    public const string Name = "DynamoDb Local";
}
