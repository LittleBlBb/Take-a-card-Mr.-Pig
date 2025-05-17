using System.ComponentModel.DataAnnotations;

namespace CardGameAPI.Models;

public class Move
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public Session? Session { get; set; }
    public int MoveNumber { get; set; }
    
    [Required]
    [StringLength(10)]
    public required string PlayerRequest { get; set; } 
    
    public bool Success { get; set; }
}

public class MoveDto
{
    [Required]
    [StringLength(10)]
    public required string Rank { get; set; }
    
    public bool EnableMemory { get; set; }
}