using Microsoft.EntityFrameworkCore;
using StockNet.Api.Domain.Entities;

namespace StockNet.Api.Infrastructure.Persistence;

public sealed class StockNetDbContext(DbContextOptions<StockNetDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<MessageThread> MessageThreads => Set<MessageThread>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<ProcessedStripeEvent> ProcessedStripeEvents => Set<ProcessedStripeEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StockNetDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
