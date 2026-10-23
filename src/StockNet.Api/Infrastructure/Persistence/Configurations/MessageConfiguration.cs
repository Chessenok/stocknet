using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockNet.Api.Domain.Entities;

namespace StockNet.Api.Infrastructure.Persistence.Configurations;

public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages", table =>
        {
            table.HasCheckConstraint("CK_Messages_Text_NotEmpty", "length(trim(\"Text\")) > 0");
        });

        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();
        builder.Property(message => message.Text).HasMaxLength(5000).IsRequired();
        builder.Property(message => message.SentAt).IsRequired();

        builder.HasIndex(message => new { message.ThreadId, message.SentAt });
        builder.HasIndex(message => message.SenderCompanyId);
    }
}
