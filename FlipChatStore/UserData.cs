using System.ComponentModel.DataAnnotations;

namespace FlipChatStore;

public class UserData
{
    public string ConnectionId { get; set; }

    [Required]
    [RegularExpression("^(male|female|other)$", ErrorMessage = "Invalid gender")]
    public string Gender { get; set; }

    [Required]
    [RegularExpression("^(under14|15-17|18plus)$", ErrorMessage = "Invalid age group")]
    public string Age { get; set; }

    [Required]
    [RegularExpression("^(male|female|any)$", ErrorMessage = "Invalid preferred gender")]
    public string CompanionGender { get; set;  }

    [Required]
    [RegularExpression("^(under14|15-17|18plus)$", ErrorMessage = "Invalid preferred age")]
    public string CompanionAge { get; set;  }

    [RegularExpression("^(light|dark)$", ErrorMessage = "Invalid theme")]
    public string Theme { get; set; } = "light";

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}