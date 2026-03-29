using Xunit;

namespace PrintIt.Tests.Infrastructure;

// Ensures all tests in this collection share the same Postgres container instance.
[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "db";
}
