using Microsoft.AspNetCore.Mvc;
using CardGameAPI.Data;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using CardGameAPI.Models;
namespace CardGameAPI.Controllers;

[ApiController]
[Route("api/sessions")]
public class SessionsController : ControllerBase
{
    private readonly GameContext _context;
    private readonly HttpClient _prologClient;

    public class CreateSessionDto
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        [Range(0, 3)]
        public int Difficulty { get; set; } = 1;
    }

    public SessionsController(GameContext context, IHttpClientFactory clientFactory)
    {
        _context = context;
        _prologClient = clientFactory.CreateClient("Prolog");
    }

    [HttpPost]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionDto dto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(e => e.Value.Errors.Count > 0)
                .ToDictionary(
                    e => e.Key,
                    e => e.Value.Errors.Select(error => error.ErrorMessage).ToArray()
                );
            return BadRequest(new { Title = "Validation failed", Errors = errors });
        }

        var user = await _context.Users.FindAsync(dto.UserId);
        if (user == null) return NotFound("User not found");

        var prologResponse = await _prologClient.PostAsJsonAsync(
            "set_difficulty",
            new
            {
                difficulty = dto.Difficulty switch
                {
                    0 => "easy",
                    1 => "medium",
                    2 => "hard",
                    3 => "expert",
                    _ => "medium"
                }
            });

        if (!prologResponse.IsSuccessStatusCode)
            return BadRequest("Failed to set difficulty in Prolog service");

        var prologResult = await prologResponse.Content.ReadFromJsonAsync<PrologDifficultyResponse>();
        if (prologResult == null || prologResult.Result == "error")
            return BadRequest(prologResult?.Message ?? "Prolog error");

        var session = new Session
        {
            UserId = dto.UserId,
            Difficulty = dto.Difficulty
        };

        _context.Sessions.Add(session);
        await _context.SaveChangesAsync();

        return Ok(session);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetSession(int id)
    {
        var session = await _context.Sessions
            .Include(s => s.Moves)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (session == null) return NotFound();

        var prologResponse = await _prologClient.GetAsync("status");
        if (!prologResponse.IsSuccessStatusCode)
            return BadRequest("Failed to get game status from Prolog");

        var prologStatus = await prologResponse.Content.ReadFromJsonAsync<PrologStatusResponse>();
        if (prologStatus == null) return BadRequest("Invalid Prolog response");

        session.PlayerHand = JsonSerializer.Serialize(prologStatus.PlayerHand);
        session.BotHand = JsonSerializer.Serialize(prologStatus.PlayerHand);
        session.PlayerScore = prologStatus.PlayerScore;
        session.BotScore = prologStatus.BotScore;
        if (prologStatus.Winner != null)
            session.Winner = prologStatus.Winner;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            Session = session,
            PlayerHand = prologStatus.PlayerHand,
            BotHandLength = prologStatus.BotHandLength,
            PlayerScore = prologStatus.PlayerScore,
            BotScore = prologStatus.BotScore,
            Turn = prologStatus.Turn,
            Winner = prologStatus.Winner
        });
    }

    [HttpPost("{id}/terminate")]
    public async Task<IActionResult> TerminateSession(int id)
    {
        var session = await _context.Sessions.FindAsync(id);
        if (session == null) return NotFound();

        var prologResponse = await _prologClient.GetAsync("status");
        if (prologResponse.IsSuccessStatusCode)
        {
            var prologStatus = await prologResponse.Content.ReadFromJsonAsync<PrologStatusResponse>();
            if (prologStatus != null)
            {
                session.PlayerHand = JsonSerializer.Serialize(prologStatus.PlayerHand);
                session.BotHand = JsonSerializer.Serialize(prologStatus.PlayerHand);
                session.PlayerScore = prologStatus.PlayerScore;
                session.BotScore = prologStatus.BotScore;
                session.Winner = prologStatus.Winner ?? (prologStatus.PlayerScore > prologStatus.BotScore ? "player" :
                                prologStatus.PlayerScore < prologStatus.BotScore ? "bot" : "draw");
            }
        }

        session.EndTime = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

public class PrologDifficultyResponse
{
    public string Result { get; set; } = string.Empty;
    public string? Message { get; set; }
}

public class PrologStatusResponse
{
    public List<Card> PlayerHand { get; set; } = new List<Card>();
    public int BotHandLength { get; set; }
    public int PlayerScore { get; set; }
    public int BotScore { get; set; }
    public string Turn { get; set; } = string.Empty;
    public string? Winner { get; set; }
}