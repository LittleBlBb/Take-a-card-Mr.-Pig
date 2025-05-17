using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

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
    public int Difficulty { get; set; } = 1; // // 0-Easy, 1-Medium, 2-Hard, 3-Expert
    public List<Move>? Moves { get; set; }
}

public class CreateSessionDto
{
    [Required]
    public int UserId { get; set; }
    
    [Required]
    [Range(0, 3)] 
    public int Difficulty { get; set; } = 1; 
}