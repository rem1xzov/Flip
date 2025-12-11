using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FlipChatStore;

namespace FlipStoreAnon.DbContext.Configuration;

public class UserDataConfiguration : IEntityTypeConfiguration<UserData>
{
    [Obsolete]
    public void Configure(EntityTypeBuilder<UserData> builder)
    {
        // Таблица в БД
        builder.ToTable("user_sessions");
        
        // Первичный ключ
        builder.HasKey(u => u.ConnectionId);
        
        // Поля
        builder.Property(u => u.ConnectionId)
            .HasColumnName("connection_id")
            .HasMaxLength(255)
            .IsRequired();
            
        builder.Property(u => u.Gender)
            .HasColumnName("gender")
            .HasMaxLength(10)
            .IsRequired()
            .HasConversion<string>();
            
        builder.Property(u => u.Age)
            .HasColumnName("age_group")
            .HasMaxLength(10)
            .IsRequired()
            .HasConversion<string>();
            
        builder.Property(u => u.CompanionGender)
            .HasColumnName("preferred_gender")
            .HasMaxLength(10)
            .IsRequired()
            .HasConversion<string>();
            
        builder.Property(u => u.CompanionAge)
            .HasColumnName("preferred_age")
            .HasMaxLength(10)
            .IsRequired()
            .HasConversion<string>();
            
        builder.Property(u => u.Theme)
            .HasColumnName("theme")
            .HasMaxLength(10)
            .HasDefaultValue("light")
            .HasConversion<string>();
            
        builder.Property(u => u.JoinedAt)
            .HasColumnName("joined_at")
            .IsRequired();

        // Индексы для быстрого поиска
        builder.HasIndex(u => u.JoinedAt)
            .HasDatabaseName("ix_user_sessions_joined_at");
            
        builder.HasIndex(u => new { u.Gender, u.Age })
            .HasDatabaseName("ix_user_sessions_gender_age");
            
        builder.HasIndex(u => new { u.CompanionGender, u.CompanionAge })
            .HasDatabaseName("ix_user_sessions_preferences");

        // Ограничения CHECK для валидации на уровне БД
        builder.HasCheckConstraint("ck_user_sessions_gender", "gender IN ('male', 'female', 'other')");
        builder.HasCheckConstraint("ck_user_sessions_age", "age_group IN ('under14', '15-17', '18plus')");
        builder.HasCheckConstraint("ck_user_sessions_pref_gender", "preferred_gender IN ('male', 'female', 'any')");
        builder.HasCheckConstraint("ck_user_sessions_pref_age", "preferred_age IN ('under14', '15-17', '18plus')");
        builder.HasCheckConstraint("ck_user_sessions_theme", "theme IN ('light', 'dark')");
    }
}