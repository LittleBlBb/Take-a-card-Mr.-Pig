using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CardGameAPI.Data;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;

namespace CardGameAPI.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly GameContext _context;

    public UsersController(GameContext context) => _context = context;

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(int id)
    {
        var userIdFromToken = int.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
        if (id != userIdFromToken) return Unauthorized("Access denied");

        var user = await _context.Users
            .Where(u => u.Id == id)
            .Select(u => new { u.Id, u.Username })
            .FirstOrDefaultAsync();

        return user == null ? NotFound() : Ok(user);
    }

    [HttpGet("{id}/stats")]
    public async Task<IActionResult> GetUserStats(int id)
    {
        var userIdFromToken = int.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
        if (id != userIdFromToken) return Unauthorized("Access denied");

        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        var stats = await _context.Sessions
            .Where(s => s.UserId == id && s.EndTime != null)
            .GroupBy(s => s.Winner)
            .Select(g => new
            {
                Outcome = g.Key,
                Count = g.Count()
            })
            .ToListAsync();

        var totalGames = stats.Sum(s => s.Count);
        var wins = stats.FirstOrDefault(s => s.Outcome == "player")?.Count ?? 0;
        var losses = stats.FirstOrDefault(s => s.Outcome == "bot")?.Count ?? 0;
        var draws = stats.FirstOrDefault(s => s.Outcome == "draw")?.Count ?? 0;

        return Ok(new
        {
            TotalGames = totalGames,
            Wins = wins,
            Losses = losses,
            Draws = draws
        });
    }

    [HttpGet("{id}/avg_moves")]
    public async Task<IActionResult> GetAverageMoves(int id)
    {
        var userIdFromToken = int.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
        if (id != userIdFromToken) return Unauthorized("Access denied");

        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        var avgMoves = await _context.Sessions
            .Where(s => s.UserId == id && s.EndTime != null)
            .Select(s => s.Moves != null ? s.Moves.Count : 0)
            .DefaultIfEmpty(0)
            .AverageAsync();

        return Ok(new { AverageMoves = avgMoves });
    }
}