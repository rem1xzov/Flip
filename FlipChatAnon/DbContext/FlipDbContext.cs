using FlipChatStore;
using FlipStoreAnon.DbContext.Configuration;
using Microsoft.EntityFrameworkCore;
namespace FlipStoreAnon.DbContext;
public class FlipDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public FlipDbContext(DbContextOptions<FlipDbContext> options) : base(options) { }
    
    public DbSet<UserData> UserSessions { get; set; }
    public DbSet<ChatRoom> ChatRooms { get; set; }
    public DbSet<ChatMessage> Messages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserDataConfiguration());
        modelBuilder.ApplyConfiguration(new ChatRoomConfiguration()); 
        modelBuilder.ApplyConfiguration(new ChatMessageConfiguration());
    }
    
}