namespace FlipChatStore;

public class ChatMessage
{
    public long MessageId { get; set; }
    public string RoomId { get; set; }
    public string Text { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string SenderConnectionId { get; set; } 
    public UserData User { get; set; }
}