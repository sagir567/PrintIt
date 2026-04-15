using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PrintIt.Infrastructure.Persistence;

namespace PrintIt.Tests.Infrastructure;

// Boots the API in-memory for integration tests and overrides DB connection.
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public ApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public new HttpClient CreateClient()
    {
        return base.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var testAuthConfig = new Dictionary<string, string?>
            {
                ["AuthCookie:Name"] = "printit_admin_auth",
                ["AuthCookie:Path"] = "/",
                ["AuthCookie:HttpOnly"] = "true",
                ["AuthCookie:IsEssential"] = "true",
                ["AuthCookie:SameSite"] = "Lax",
                ["AuthCookie:Secure"] = "false",
                ["Jwt:Issuer"] = "PrintIt.Tests",
                ["Jwt:Audience"] = "PrintIt.Tests.Admin",
                ["Jwt:SigningKey"] = "TEST_ONLY_CHANGE_ME_12345678901234567890",
                ["Jwt:AccessTokenMinutes"] = "120",
                ["AdminBootstrap:Email"] = "admin@test.local",
                ["AdminBootstrap:Password"] = "Admin123!",
                ["Cors:AllowedOrigins:0"] = "http://localhost:5173"
            };

            configBuilder.AddInMemoryCollection(testAuthConfig);
        });

        builder.ConfigureServices(services =>
        {
            // Remove the existing AppDbContext registration (the "real" one).
            var descriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>));

            if (descriptor is not null)
                services.Remove(descriptor);

            // Register AppDbContext using the test container DB.
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_connectionString));

            // Ensure DB is created/migrated for tests.
            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        });
    }
}
