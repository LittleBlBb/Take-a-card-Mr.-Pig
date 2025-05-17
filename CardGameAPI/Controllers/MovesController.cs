using Microsoft.AspNetCore.Mvc;
using CardGameAPI.Models;
using CardGameAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace CardGameAPI.Controllers;

[ApiController]
[Route("api/sessions/{sessionId}/moves")]
public class MovesController : ControllerBase
{
    private readonly GameContext _context;
    private readonly HttpClient _prologClient;

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

        // Запрос к Prolog
        var prologResponse = await _prologClient.PostAsJsonAsync(
            "move", 
            new
            {
                rank = dto.Rank,
                enable_memory = dto.EnableMemory // Фича для запоминания ходов из FR1.6. Только в БД не записывается, тут Prolog-сервер должен обрабатывать enable_memory.
            });

        if (!prologResponse.IsSuccessStatusCode)
            return BadRequest("Prolog move failed");

        // Сохранение хода
        var moveNumber = await _context.Moves
            .Where(m => m.SessionId == sessionId)
            .CountAsync() + 1;

        var move = new Move 
        { 
            SessionId = sessionId,
            MoveNumber = moveNumber,
            PlayerRequest = dto.Rank,
            Success = true 
        };

        _context.Moves.Add(move);
        await _context.SaveChangesAsync();

        // Prolog  возвращает JSON с полями result, player_hand, player_score, bot_score
        // UI  отправляет POST-запрос на этот эндпоинт с MoveDto и отображает результат хода


        return Ok(move);
    }
}