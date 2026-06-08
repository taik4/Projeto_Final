namespace BankingCore.IntegrationTests.Fixtures;

/// <summary>
/// Collection attribute que agrupa testes que compartilham a mesma instância do container MySQL.
/// Todos os testes nesta collection serão executados sequencialmente.
/// </summary>
[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<MySqlDatabaseFixture>
{
    // This class has no code, and is only used to define the collection
}
