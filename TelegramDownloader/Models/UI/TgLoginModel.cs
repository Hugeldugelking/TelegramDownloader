using System.ComponentModel.DataAnnotations;

namespace TelegramDownloader.Models.UI;

public class TgLoginModel
{
    [Required]
    public string? api_id { get;       set; }
    [Required]
    public string? api_hash     { get; set; }
    [Required]
    public string? phone_number { get; set; }
}