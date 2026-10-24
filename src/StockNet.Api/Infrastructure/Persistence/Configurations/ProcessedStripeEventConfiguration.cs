using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockNet.Api.Domain.Entities;

namespace StockNet.Api.Infrastructure.Persistence.Configurations;

public sealed class ProcessedStripeEventConfiguration : IEntityTypeConfiguration<ProcessedStripeEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedStripeEvent> builder)
    {
        builder.ToTable("ProcessedStripeEvents");

        builder.HasKey(stripeEvent => stripeEvent.EventId);
        builder.Property(stripeEvent => stripeEvent.EventId).HasMaxLength(255).ValueGeneratedNever();
        builder.Property(stripeEvent => stripeEvent.EventType).HasMaxLength(255).IsRequired();
        builder.Property(stripeEvent => stripeEvent.ProcessedAtUtc).IsRequired();

        builder.HasIndex(stripeEvent => stripeEvent.ProcessedAtUtc);
    }
}
