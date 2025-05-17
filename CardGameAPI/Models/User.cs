using System.ComponentModel.DataAnnotations;

namespace CardGameAPI.Models;

public class User
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(50)]
    public required string Username { get; set; }
    
    [Required]
    public required string PasswordHash { get; set; }
    
    public List<Session>? Sessions { get; set; }
}