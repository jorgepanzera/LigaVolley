using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LigaVolley.Infrastructure.Persistence;

public sealed class LigaVolleyDbContextFactory : IDesignTimeDbContextFactory<LigaVolleyDbContext>
{
    public LigaVolleyDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__LigaVolley")
            ?? "Server=(localdb)\\mssqllocaldb;Database=LigaVolley;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<LigaVolleyDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new LigaVolleyDbContext(options);
    }
}
