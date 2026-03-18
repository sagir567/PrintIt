using System.Threading.Tasks;
using Testcontainers.PostgreSql;

namespace PrintIt.Tests.Infrastructure;

// This fixture owns the PostgreSQL container lifecycle for integration tests.
// xUnit will create it once (per test collection) and dispose it at the end.
public sealed class PostgresFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; }

    public string ConnectionString => Container.GetConnectionString();

    public PostgresFixture()
    {
// Build the container with an explicit image (required by newer Testcontainers versions).
        Container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("printit_test")
            .WithUsername("printit")
            .WithPassword("printit")
            .Build();
        
    }

    // Called by xUnit before any tests that use this fixture run.
    public async Task InitializeAsync()
    {
        await Container.StartAsync();
    }

    // Called by xUnit after all tests that use this fixture have finished.
    public async Task DisposeAsync()
    {
        await Container.DisposeAsync();
    }
}
