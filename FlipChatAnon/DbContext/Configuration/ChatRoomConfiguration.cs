using FlipChatStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlipStoreAnon.DbContext;

public class ChatRoomConfiguration : IEntityTypeConfiguration<ChatRoom>
{
    public void Configure(EntityTypeBuilder<ChatRoom> builder)
    {
        builder.ToTable("chat_rooms");
        
        builder.HasKey(r => r.RoomId);
        
        builder.Property(r => r.RoomId)
            .HasColumnName("room_id")
            .HasMaxLength(36)
            .IsRequired();
            
        builder.Property(r => r.User1ConnectionId)
            .HasColumnName("user1_connection_id")
            .HasMaxLength(255)
            .IsRequired();
            
        builder.Property(r => r.User2ConnectionId)
            .HasColumnName("user2_connection_id")
            .HasMaxLength(255)
            .IsRequired();
            
        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Связи через явные FK
        builder.HasOne(r => r.User1)
            .WithMany()
            .HasForeignKey(r => r.User1ConnectionId)
            .HasPrincipalKey(u => u.ConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasOne(r => r.User2)
            .WithMany()
            .HasForeignKey(r => r.User2ConnectionId)
            .HasPrincipalKey(u => u.ConnectionId)
            .OnDelete(DeleteBehavior.Cascade);

        // ✅ ИЗМЕНЕНО: Убраны уникальные индексы, которые вызывали ошибки
        builder.HasIndex(r => r.CreatedAt)
            .HasDatabaseName("ix_chat_rooms_created_at");
            
        // Обычные индексы вместо уникальных
        builder.HasIndex(r => r.User1ConnectionId)
            .HasDatabaseName("ix_chat_rooms_user1");
            
        builder.HasIndex(r => r.User2ConnectionId)
            .HasDatabaseName("ix_chat_rooms_user2");
    }
}