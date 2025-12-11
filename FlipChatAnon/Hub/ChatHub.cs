using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Text.RegularExpressions;
using Serilog;
using System.ComponentModel.DataAnnotations;
using FlipChatStore;

namespace ChatAnon.Hubs;

public class ChatHub : Hub<ChatHub.IChatClient>
{
    public interface IChatClient
    {
      public Task ReceiveMessage(ChatMessage message);
      Task MatchFound(UserData companion, string roomId); // Отправляем данные найденного собеседника и roomId
      Task UserJoined(string message);
      Task UserLeft(string message);
      Task WaitingForCompanion(); // Уведомление о поиске
      Task TypingStatus(bool isTyping); // Индикатор набора текста
    }

 
     private static readonly List<UserData> _waitingUsers = new();
     private static readonly Dictionary<string, ChatRoom> _activeRooms = new(); // roomId -> ChatRoom
     private static readonly Dictionary<string, string> _userRooms = new(); // connectionId -> roomId
     private static readonly Dictionary<string, DateTime> _lastMessageTime = new(); // connectionId -> last message time
    private static readonly Dictionary<string, int> _messageCount = new(); // connectionId -> message count per minute

     private readonly IMemoryCache _cache;
     private readonly ILogger<ChatHub> _logger;
     
     // Константы для валидации
     private const int MAX_MESSAGE_LENGTH = 1000;
     private const int MAX_MESSAGES_PER_MINUTE = 30;
     private const int MIN_MESSAGE_INTERVAL_MS = 1000; // 1 секунда между сообщениями
     private const int MAX_CONNECTIONS_PER_IP = 5;
     private static readonly Dictionary<string, int> _ipConnectionCount = new();
     
     // Регулярные выражения для валидации
     private static readonly Regex HtmlTagRegex = new(@"<[^>]*>", RegexOptions.Compiled);
     private static readonly Regex ScriptRegex = new(@"<script[^>]*>.*?</script>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
     private static readonly Regex SuspiciousPatterns = new(@"(javascript:|data:|vbscript:|onload=|onerror=)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

     public ChatHub(IMemoryCache cache, ILogger<ChatHub> logger)
     {
         _cache = cache;
         _logger = logger;
     } 
     

     public override async Task OnConnectedAsync()
     {
         var connectionId = Context.ConnectionId;
         var remoteIp = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();
         
         // Проверка лимита подключений с одного IP
         if (!string.IsNullOrEmpty(remoteIp))
         {
             lock (_ipConnectionCount)
             {
                 if (_ipConnectionCount.TryGetValue(remoteIp, out var count))
                 {
                     if (count >= MAX_CONNECTIONS_PER_IP)
                     {
                         _logger.LogWarning("Too many connections from IP {RemoteIP}: {Count}", remoteIp, count);
                         Context.Abort();
                         return;
                     }
                     _ipConnectionCount[remoteIp] = count + 1;
                 }
                 else
                 {
                     _ipConnectionCount[remoteIp] = 1;
                 }
             }
         }
         
         _logger.LogInformation("User connected: {ConnectionId} from {RemoteIP}", connectionId, remoteIp);
         
         await Clients.Caller.UserJoined("Подключено к чату");
         await base.OnConnectedAsync();
     }

     public override async Task OnDisconnectedAsync(Exception exception)
     {
         var connectionId = Context.ConnectionId;
         var remoteIp = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();

         _logger.LogInformation("User disconnected: {ConnectionId} from {RemoteIP}", connectionId, remoteIp);

         // Если пользователь был в комнате - уведомляем собеседника
         if (_userRooms.TryGetValue(connectionId, out var roomId))
         {
             if (_activeRooms.TryGetValue(roomId, out var room))
             {
                 var companion = room.User1.ConnectionId == connectionId ? room.User2 : room.User1;
                 await Clients.Client(companion.ConnectionId).UserLeft("Собеседник покинул чат");

                 _logger.LogInformation("Chat room {RoomId} closed due to user disconnect", roomId);
                 
                 // Удаляем комнату
                 _activeRooms.Remove(roomId);
             }
             _userRooms.Remove(connectionId);
         }

         // Удаляем из очереди ожидания
         var removedCount = _waitingUsers.RemoveAll(u => u.ConnectionId == connectionId);
         if (removedCount > 0)
         {
             _logger.LogInformation("Removed {Count} users from waiting queue", removedCount);
         }

         // Очищаем статистику сообщений
         _lastMessageTime.Remove(connectionId);
         _messageCount.Remove(connectionId);

         // Уменьшаем счетчик подключений с IP
         if (!string.IsNullOrEmpty(remoteIp))
         {
             lock (_ipConnectionCount)
             {
                 if (_ipConnectionCount.TryGetValue(remoteIp, out var count))
                 {
                     if (count > 1)
                         _ipConnectionCount[remoteIp] = count - 1;
                     else
                         _ipConnectionCount.Remove(remoteIp);
                 }
             }
         }

         await base.OnDisconnectedAsync(exception);
     }

     public async Task JoinChat(UserData userData)
     {
         try
         {
             // Валидация входных данных
             var validationResults = new List<ValidationResult>();
             var validationContext = new ValidationContext(userData);
             
             if (!Validator.TryValidateObject(userData, validationContext, validationResults, true))
             {
                 _logger.LogWarning("Invalid user data received from {ConnectionId}: {Errors}", 
                     Context.ConnectionId, string.Join(", ", validationResults.Select(r => r.ErrorMessage)));
                 
                 await Clients.Caller.UserLeft("Неверные данные пользователя");
                 return;
             }

             // Заполняем ConnectionId
             userData.ConnectionId = Context.ConnectionId;
             userData.JoinedAt = DateTime.UtcNow;

             _logger.LogInformation("User {ConnectionId} joining chat with preferences: Gender={Gender}, Age={Age}, CompanionGender={CompanionGender}, CompanionAge={CompanionAge}",
                 Context.ConnectionId, userData.Gender, userData.Age, userData.CompanionGender, userData.CompanionAge);

             // Ищем подходящего собеседника
             var matchedCompanion = FindMatchingCompanion(userData);

             var stringuserData = JsonSerializer.Serialize(userData);
             _cache.Set(Context.ConnectionId, stringuserData, TimeSpan.FromHours(1)); // Данные пользователя хранятся 1 час

             if (matchedCompanion != null)
             {
                 // Нашли пару - создаем чат комнату
                 await CreateChatRoom(userData, matchedCompanion);
             }
             else
             {
                 // Не нашли - добавляем в очередь ожидания
                 _waitingUsers.Add(userData);
                 _logger.LogInformation("User {ConnectionId} added to waiting queue. Total waiting: {Count}", 
                     Context.ConnectionId, _waitingUsers.Count);
                 await Clients.Caller.WaitingForCompanion();
             }
         }
         catch (Exception ex)
         {
             _logger.LogError(ex, "Error in JoinChat for connection {ConnectionId}", Context.ConnectionId);
             await Clients.Caller.UserLeft("Ошибка при подключении к чату");
         }
     }

     public async Task SendMessage(string messageText)
     {
         try
         {
             var connectionId = Context.ConnectionId;
             
             // Валидация сообщения
             if (!ValidateMessage(messageText, connectionId))
             {
                 return;
             }

             // Проверяем, что пользователь находится в комнате
             if (!_userRooms.TryGetValue(connectionId, out var roomId))
             {
                 _logger.LogWarning("User {ConnectionId} tried to send message but not in any room", connectionId);
                 await Clients.Caller.UserLeft("Вы не находитесь в чате");
                 return;
             }

             // Получаем данные пользователя из кэша
             if (!_cache.TryGetValue(connectionId, out string userDataString) || string.IsNullOrEmpty(userDataString))
             {
                 _logger.LogWarning("User data not found in cache for {ConnectionId}", connectionId);
                 await Clients.Caller.UserLeft("Сессия истекла");
                 return;
             }

             var userData = JsonSerializer.Deserialize<UserData>(userDataString);
             if (userData == null)
             {
                 _logger.LogWarning("Failed to deserialize user data for {ConnectionId}", connectionId);
                 await Clients.Caller.UserLeft("Ошибка данных пользователя");
                 return;
             }

             // Создаем и отправляем сообщение
             var message = new ChatMessage
             {
                 RoomId = roomId,
                 Text = SanitizeMessage(messageText),
                 Timestamp = DateTime.UtcNow
             };

             _logger.LogInformation("Message sent in room {RoomId} by {ConnectionId}: {MessageLength} chars", 
                 roomId, connectionId, messageText.Length);

            // Отправляем сообщение только собеседнику, чтобы у отправителя не было дублей
            await Clients.OthersInGroup(roomId).ReceiveMessage(message);
         }
         catch (Exception ex)
         {
             _logger.LogError(ex, "Error sending message from {ConnectionId}", Context.ConnectionId);
             await Clients.Caller.UserLeft("Ошибка при отправке сообщения");
         }
     }

    public async Task Typing(bool isTyping)
    {
        // Сообщаем собеседнику о наборе текста
        if (_userRooms.TryGetValue(Context.ConnectionId, out var roomId) &&
            _activeRooms.TryGetValue(roomId, out var room))
        {
            var companion = room.User1.ConnectionId == Context.ConnectionId ? room.User2 : room.User1;
            await Clients.Client(companion.ConnectionId).TypingStatus(isTyping);
        }
    }

    public async Task FindNewCompanion()
    {
        var connectionId = Context.ConnectionId;
        if (_userRooms.TryGetValue(connectionId, out var roomId) &&
            _activeRooms.TryGetValue(roomId, out var room))
        {
            var companion = room.User1.ConnectionId == connectionId ? room.User2 : room.User1;
            // Уведомляем собеседника и закрываем комнату
            await Clients.Client(companion.ConnectionId).UserLeft("Собеседник покинул чат");
            _activeRooms.Remove(roomId);
            _userRooms.Remove(companion.ConnectionId);
            _userRooms.Remove(connectionId);
            await Groups.RemoveFromGroupAsync(companion.ConnectionId, roomId);
            await Groups.RemoveFromGroupAsync(connectionId, roomId);
            // Собеседника возвращаем в очередь ожидания
            _waitingUsers.Add(companion);
        }

        // Текущего пользователя добавляем в очередь ожидания
        if (!_cache.TryGetValue(connectionId, out string userDataString))
        {
            await Clients.Caller.UserLeft("Сессия истекла");
            return;
        }
        var user = !string.IsNullOrEmpty(userDataString) ? JsonSerializer.Deserialize<UserData>(userDataString) : null;
        if (user != null)
        {
            _waitingUsers.Add(user);
            await Clients.Caller.WaitingForCompanion();
        }
    }

     private UserData FindMatchingCompanion(UserData newUser)
     {
         return _waitingUsers.FirstOrDefault(waitingUser =>
             IsMutualMatch(newUser, waitingUser));
     }

     private bool IsMutualMatch(UserData user1, UserData user2)
     {
         // User1 хочет общаться с User2 И User2 хочет общаться с User1
         return IsCompatible(user1, user2) && IsCompatible(user2, user1);
     }

     private bool IsCompatible(UserData seeker, UserData target)
     {
         // Проверяем пол (если указано предпочтение)
         if (seeker.CompanionGender != "any" && seeker.CompanionGender != target.Gender)
             return false;

         // Проверяем возрастную группу
         if (seeker.CompanionAge != target.Age)
             return false;

         return true;
     }

     private async Task CreateChatRoom(UserData user1, UserData user2)
     {
         // Удаляем из очереди
         _waitingUsers.Remove(user2);

         // Создаем комнату
         var roomId = Guid.NewGuid().ToString();
         var room = new ChatRoom
         {
             RoomId = roomId,
             User1 = user1,
             User2 = user2
         };

         // Сохраняем комнату
         _activeRooms[roomId] = room;
         _userRooms[user1.ConnectionId] = roomId;
         _userRooms[user2.ConnectionId] = roomId;

         // Добавляем в группы
         await Groups.AddToGroupAsync(user1.ConnectionId, roomId);
         await Groups.AddToGroupAsync(user2.ConnectionId, roomId);

         // Уведомляем обоих о найденном собеседнике
        await Clients.Caller.MatchFound(user2, roomId);
        await Clients.Client(user2.ConnectionId).MatchFound(user1, roomId);

         // Приветственное сообщение
         await Clients.Group(roomId).ReceiveMessage(new ChatMessage
         {
             RoomId = roomId,
             Text = "Собеседник найден! Начинайте общение!",
             Timestamp = DateTime.UtcNow
         });
     }

     private bool ValidateMessage(string messageText, string connectionId)
     {
         // Проверка на null или пустую строку
         if (string.IsNullOrWhiteSpace(messageText))
         {
             _logger.LogWarning("Empty message from {ConnectionId}", connectionId);
             return false;
         }

         // Проверка длины сообщения
         if (messageText.Length > MAX_MESSAGE_LENGTH)
         {
             _logger.LogWarning("Message too long from {ConnectionId}: {Length} chars", connectionId, messageText.Length);
             return false;
         }

         // Проверка на подозрительные паттерны
         if (ScriptRegex.IsMatch(messageText) || SuspiciousPatterns.IsMatch(messageText))
         {
             _logger.LogWarning("Suspicious message content from {ConnectionId}: {Message}", connectionId, messageText);
             return false;
         }

         // Проверка частоты сообщений
         var now = DateTime.UtcNow;
         if (_lastMessageTime.TryGetValue(connectionId, out var lastTime))
         {
             var timeSinceLastMessage = now - lastTime;
             if (timeSinceLastMessage.TotalMilliseconds < MIN_MESSAGE_INTERVAL_MS)
             {
                 _logger.LogWarning("Message sent too quickly from {ConnectionId}: {Interval}ms", 
                     connectionId, timeSinceLastMessage.TotalMilliseconds);
                 return false;
             }
         }

            // Проверка количества сообщений в минуту с авто-сбросом
            var cacheKey = $"rate:{connectionId}";
            if (!_cache.TryGetValue(cacheKey, out int count))
            {
                count = 0;
            }
            if (count >= MAX_MESSAGES_PER_MINUTE)
            {
                _logger.LogWarning("Rate limit exceeded for {ConnectionId}: {Count} messages", connectionId, count);
                return false;
            }
            count++;
            _cache.Set(cacheKey, count, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
            });

         _lastMessageTime[connectionId] = now;
         return true;
     }

     private string SanitizeMessage(string messageText)
     {
         // Удаляем HTML теги
         var sanitized = HtmlTagRegex.Replace(messageText, "");
         
         // Экранируем специальные символы
         sanitized = sanitized.Replace("&", "&amp;")
                             .Replace("<", "&lt;")
                             .Replace(">", "&gt;")
                             .Replace("\"", "&quot;")
                             .Replace("'", "&#x27;");

         // Обрезаем до максимальной длины
         if (sanitized.Length > MAX_MESSAGE_LENGTH)
         {
             sanitized = sanitized.Substring(0, MAX_MESSAGE_LENGTH);
         }

         return sanitized.Trim();
     }

     // Статические методы для получения статистики
     public static int GetActiveRoomsCount()
     {
         return _activeRooms.Count;
     }

     public static int GetWaitingUsersCount()
     {
         return _waitingUsers.Count;
     }

     public static object GetDetailedStats()
     {
         return new
         {
             ActiveRooms = _activeRooms.Count,
             WaitingUsers = _waitingUsers.Count,
             UserRooms = _userRooms.Count,
             LastMessageTimes = _lastMessageTime.Count,
             MessageCounts = _messageCount.Count,
             Timestamp = DateTime.UtcNow
         };
     }
}