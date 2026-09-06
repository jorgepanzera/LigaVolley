using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace LigaVolley.Infrastructure.Persistence;

public sealed class LigaVolleyDbContextFactory : IDesignTimeDbContextFactory<LigaVolleyDbContext>
{
    private const string ApiUserSecretsId = "a8224f8c-3be1-4bca-82dc-2e1fafbdeb77";

    public LigaVolleyDbContext CreateDbContext(string[] args)
    {
        var secretsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "UserSecrets", ApiUserSecretsId, "secrets.json");
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(secretsPath, optional: true, reloadOnChange: false)
            .Build();

        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__LigaVolley")
            ?? configuration.GetConnectionString("LigaVolley")
            ?? "Server=(localdb)\\mssqllocaldb;Database=LigaVolley;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<LigaVolleyDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new LigaVolleyDbContext(options);
    }
}
