using FlipChatStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlipStoreAnon.DbContext;

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("messages");
        
        builder.HasKey(m => m.MessageId);
        
        builder.Property(m => m.MessageId)
            .HasColumnName("message_id")
            .ValueGeneratedOnAdd();
            
        builder.Property(m => m.RoomId)
            .HasColumnName("room_id")
            .HasMaxLength(36)
            .IsRequired();
            
        builder.Property(m => m.Text)
            .HasColumnName("message_text")
            .HasMaxLength(4000)
            .IsRequired();
            
        builder.Property(m => m.Timestamp)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(m => m.SenderConnectionId)
            .HasColumnName("sender_connection_id")
            .HasMaxLength(255)
            .IsRequired();

        // Внешний ключ на комнату
        builder.HasOne<ChatRoom>()
            .WithMany()
            .HasForeignKey(m => m.RoomId)
            .HasPrincipalKey(r => r.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        // Внешний ключ на пользователя (отправителя)
        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.SenderConnectionId)
            .HasPrincipalKey(u => u.ConnectionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Индексы
        builder.HasIndex(m => m.RoomId)
            .HasDatabaseName("ix_messages_room_id");
            
        builder.HasIndex(m => m.Timestamp)
            .HasDatabaseName("ix_messages_created_at");
            
        builder.HasIndex(m => m.SenderConnectionId)
            .HasDatabaseName("ix_messages_sender");
            
        builder.HasIndex(m => new { m.RoomId, m.Timestamp })
            .HasDatabaseName("ix_messages_room_created")
            .IsDescending(false, true);
    }
}