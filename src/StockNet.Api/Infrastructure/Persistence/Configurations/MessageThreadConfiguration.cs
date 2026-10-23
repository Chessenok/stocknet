using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockNet.Api.Domain.Entities;

namespace StockNet.Api.Infrastructure.Persistence.Configurations;

public sealed class MessageThreadConfiguration : IEntityTypeConfiguration<MessageThread>
{
    public void Configure(EntityTypeBuilder<MessageThread> builder)
    {
        builder.ToTable("MessageThreads", table =>
        {
            table.HasCheckConstraint("CK_MessageThreads_DistinctCompanies", "\"BuyerCompanyId\" <> \"SellerCompanyId\"");
        });

        builder.HasKey(thread => thread.Id);
        builder.Property(thread => thread.Id).ValueGeneratedNever();
        builder.Property(thread => thread.CreatedAt).IsRequired();

        builder.HasIndex(thread => new { thread.BuyerCompanyId, thread.CreatedAt });
        builder.HasIndex(thread => new { thread.SellerCompanyId, thread.CreatedAt });
        builder.HasIndex(thread => thread.StockId);

        builder.HasMany(thread => thread.Messages)
            .WithOne(message => message.Thread)
            .HasForeignKey(message => message.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
