using Microsoft.AspNetCore.Mvc;
using CardGameAPI.Data;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using CardGameAPI.Models;

namespace CardGameAPI.Controllers;

[ApiController]
[Route("api/sessions/{sessionId}/moves")]
public class MovesController : ControllerBase
{
    private readonly GameContext _context;
    private readonly HttpClient _prologClient;

    public class MoveDto
    {
        [Required]
        [StringLength(10)]
        public required string Rank { get; set; }

        public bool EnableMemory { get; set; }
    }

    public MovesController(GameContext context, IHttpClientFactory clientFactory)
    {
        _context = context;
        _prologClient = clientFactory.CreateClient("Prolog");
    }

    [HttpPost]
    public async Task<IActionResult> AddMove(int sessionId, [FromBody] MoveDto dto)
    {
        var session = await _context.Sessions.FindAsync(sessionId);
        if (session == null || session.EndTime != null)
            return BadRequest("Invalid session");

        var prologResponse = await _prologClient.PostAsJsonAsync(
            "move",
            new { rank = dto.Rank, enable_memory = dto.EnableMemory });

        if (!prologResponse.IsSuccessStatusCode)
            return BadRequest("Prolog move failed");

        var prologResult = await prologResponse.Content.ReadFromJsonAsync<PrologMoveResponse>();
        if (prologResult == null || prologResult.Result == "error")
            return BadRequest(prologResult?.Message ?? "Prolog error");

        var moveNumber = await _context.Moves
            .Where(m => m.SessionId == sessionId)
            .CountAsync() + 1;

        var move = new Move
        {
            SessionId = sessionId,
            MoveNumber = moveNumber,
            PlayerRequest = dto.Rank,
            Success = prologResult.Result != "retry" && prologResult.Result != "invalid_rank"
        };

        session.PlayerHand = JsonSerializer.Serialize(prologResult.PlayerHand);
        session.PlayerScore = prologResult.PlayerScore;
        session.BotScore = prologResult.BotScore;
        if (prologResult.Winner != null)
            session.Winner = prologResult.Winner;

        _context.Moves.Add(move);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            Move = move,
            PlayerHand = prologResult.PlayerHand,
            PlayerScore = prologResult.PlayerScore,
            BotScore = prologResult.BotScore,
            Result = prologResult.Result,
            Winner = prologResult.Winner
        });
    }
}

public class PrologMoveResponse
{
    public string Result { get; set; } = string.Empty;
    public List<Card>? PlayerHand { get; set; }
    public int BotHandLength { get; set; }
    public int PlayerScore { get; set; }
    public int BotScore { get; set; }
    public string? Winner { get; set; }
    public string? Message { get; set; }
}