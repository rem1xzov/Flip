
namespace FlipChatStore;

public class ChatRoom
{
    public string RoomId { get; set; }
    public string User1ConnectionId { get; set; }  
    public string User2ConnectionId { get; set; }
    public UserData User1 { get; set; }
    public UserData User2 { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

}