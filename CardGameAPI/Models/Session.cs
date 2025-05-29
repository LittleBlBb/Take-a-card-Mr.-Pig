using System.ComponentModel.DataAnnotations;

namespace CardGameAPI.Models;

public class Session
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public string? Winner { get; set; }
    public int PlayerScore { get; set; }
    public int BotScore { get; set; }
    public int Difficulty { get; set; } = 1;
    public string? PlayerHand { get; set; }
    public string? BotHand { get; set; }
    public List<Move>? Moves { get; set; }
}