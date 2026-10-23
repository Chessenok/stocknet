using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace StockNet.Api.Infrastructure.Persistence;

public sealed class StockNetDesignTimeDbContextFactory : IDesignTimeDbContextFactory<StockNetDbContext>
{
    public StockNetDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<StockNetDbContext>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("StockNet");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'StockNet' is required for EF Core tooling. Configure user-secrets or ConnectionStrings__StockNet.");
        }

        var options = new DbContextOptionsBuilder<StockNetDbContext>()
            .UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.UseNetTopologySuite())
            .Options;

        return new StockNetDbContext(options);
    }
}
