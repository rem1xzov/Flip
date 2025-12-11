using ChatAnon.Hubs;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace ChatAnon.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ILogger<ChatController> _logger;

    public ChatController(ILogger<ChatController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Получить статистику чата
    /// </summary>
    [HttpGet("stats")]
    public IActionResult GetStats()
    {
        try
        {
            var stats = new
            {
                ActiveRooms = ChatHub.GetActiveRoomsCount(),
                WaitingUsers = ChatHub.GetWaitingUsersCount(),
                Timestamp = DateTime.UtcNow,
                ServerTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
            };

            _logger.LogInformation("Stats requested: {Stats}", stats);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chat stats");
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }
    
    [HttpPost("leave")]
    public async Task<IActionResult> LeaveChat([FromBody] LeaveRequest request)
    {
        try
        {
            // Здесь можно добавить логику очистки на сервере
            _logger.LogInformation("User {ConnectionId} left via API", request.ConnectionId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LeaveChat API");
            return StatusCode(500);
        }
    }

    public class LeaveRequest
    {
        public string ConnectionId { get; set; }
    }

    /// <summary>
    /// Проверить здоровье сервиса
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0"
        });
    }
}