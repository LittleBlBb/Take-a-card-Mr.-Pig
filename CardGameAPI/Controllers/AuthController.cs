using Microsoft.AspNetCore.Mvc;
using CardGameAPI.Data;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using CardGameAPI.Models;
using System.Text;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;

namespace CardGameAPI.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly GameContext _context;
    private readonly IConfiguration _config;

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
        public string? RefreshToken { get; set; }
    }

    public class RefreshTokenDto
    {
        [Required]
        public required string RefreshToken { get; set; }
    }

    public AuthController(GameContext context, IConfiguration config)
    {
        _context = context;
        _config = config;

        // Проверка JWT конфигурации
        if (string.IsNullOrEmpty(_config["Jwt:Key"]) || _config["Jwt:Key"].Length < 32)
            throw new InvalidOperationException("JWT Key must be at least 32 characters long.");
        if (string.IsNullOrEmpty(_config["Jwt:Issuer"]) || string.IsNullOrEmpty(_config["Jwt:Audience"]))
            throw new InvalidOperationException("JWT Issuer and Audience must be configured.");
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] UserRegisterDto dto)
    {
        try
        {
            if (await _context.Users.AnyAsync(u => u.Username == dto.Username))
                return BadRequest("Username already exists");

            var user = new User
            {
                Username = dto.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { user.Id, user.Username });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Registration failed: {ex.Message}");
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] UserLoginDto dto)
    {
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == dto.Username);
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                return Unauthorized("Invalid credentials");

            var accessToken = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();

            // Сохраняем refresh-токен
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            await _context.SaveChangesAsync();

            return Ok(new AuthResponseDto
            {
                Token = accessToken,
                RefreshToken = refreshToken,
                UserId = user.Id
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Login failed: {ex.Message}");
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenDto dto)
    {
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.RefreshToken == dto.RefreshToken);
            if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
                return Unauthorized("Invalid or expired refresh token");

            var accessToken = GenerateJwtToken(user);
            var newRefreshToken = GenerateRefreshToken();

            // Обновляем refresh-токен
            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            await _context.SaveChangesAsync();

            return Ok(new AuthResponseDto
            {
                Token = accessToken,
                RefreshToken = newRefreshToken,
                UserId = user.Id
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Token refresh failed: {ex.Message}");
        }
    }

    private string GenerateJwtToken(User user)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}