using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PrintIt.Infrastructure.Persistence;
using PrintIt.Tests.Infrastructure;
using Xunit;

namespace PrintIt.Tests.Smoke;

[Collection(DatabaseCollection.Name)]
public class DbSmokeTests
{
    private readonly PostgresFixture _fx;

    public DbSmokeTests(PostgresFixture fx)
    {
        _fx = fx;
    }

    [Fact]
    public async Task Can_connect_to_postgres_container()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_fx.ConnectionString)
            .Options;

        await using var db = new AppDbContext(options);

        var canConnect = await db.Database.CanConnectAsync();
        Assert.True(canConnect);
    }
}
