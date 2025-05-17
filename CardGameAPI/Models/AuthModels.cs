using System.ComponentModel.DataAnnotations;

namespace CardGameAPI.Models;

public class UserRegisterDto
{
    [Required]
    [StringLength(50)]
    public required string Username { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public required string Password { get; set; }
}

public class UserLoginDto
{
    [Required]
    public required string Username { get; set; }

    [Required]
    public required string Password { get; set; }
}

public class AuthResponseDto
{
    public required string Token { get; set; }
    public required int UserId { get; set; }
}