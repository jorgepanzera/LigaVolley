using LigaVolley.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.MsSql;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace LigaVolley.IntegrationTests;

public sealed class LigaVolleyApiFactory : IAsyncLifetime
{
    private MsSqlContainer? database;
    private WebApplicationFactory<Program>? application;
    private string databaseName = string.Empty;
    private string connectionString = string.Empty;
    private bool sharedDatabase;

    public HttpClient Client { get; private set; } = null!;
    public IServiceProvider Services => application!.Services;
    public bool DockerAvailable { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            database = new MsSqlBuilder().Build();
            await database.StartAsync();
            connectionString = database.GetConnectionString();
            DockerAvailable = true;
        }
        catch
        {
            databaseName = $"LigaVolleyIntegration_{Guid.NewGuid():N}";
            var developmentConnection = Environment.GetEnvironmentVariable("LIGAVOLLEY_TEST_CONNECTION_STRING");
            if (string.IsNullOrWhiteSpace(developmentConnection))
                connectionString = $"Server=(localdb)\\mssqllocaldb;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True";
            else
            {
                sharedDatabase = Environment.GetEnvironmentVariable("LIGAVOLLEY_USE_SHARED_TEST_DATABASE") == "1";
                var builder = new SqlConnectionStringBuilder(developmentConnection);
                if (!sharedDatabase) builder.InitialCatalog = databaseName;
                connectionString = builder.ConnectionString;
            }
        }

        application = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:LigaVolley"] = connectionString
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<LigaVolleyDbContext>>();
                services.RemoveAll<LigaVolleyDbContext>();
                services.AddDbContext<LigaVolleyDbContext>(options => options.UseSqlServer(connectionString));
            });
        });

        using var scope = application.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
        await dbContext.Database.MigrateAsync();
        Client = application.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (application is not null && !DockerAvailable && !sharedDatabase)
        {
            using var scope = application.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
            await dbContext.Database.EnsureDeletedAsync();
        }

        application?.Dispose();
        if (database is not null)
        {
            await database.DisposeAsync();
        }
    }
}
