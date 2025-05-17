using Microsoft.AspNetCore.Mvc;
using CardGameAPI.Models;
using CardGameAPI.Data;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Text.Json;

namespace CardGameAPI.Controllers;

[ApiController]
[Route("api/sessions")]
public class SessionsController : ControllerBase
{
    private readonly GameContext _context;
    private readonly HttpClient _prologClient;

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
            
            Console.WriteLine($"Ошибки валидации: {JsonSerializer.Serialize(errors)}");
            return BadRequest(new { 
                Title = "Validation failed",
                Errors = errors 
            });
        }

        // Проверка пользователя
        var user = await _context.Users.FindAsync(dto.UserId);
        if (user == null) return NotFound("User not found");

        string difficultyName = dto.Difficulty switch
        {
            0 => "easy",
            1 => "medium",
            2 => "hard",
            3 => "expert",
            _ => "medium" 
         };

        // Интеграция с Prolog
        var prologResponse = await _prologClient.PostAsJsonAsync(
            "set_difficulty", 
            new { difficulty = dto.Difficulty.ToString().ToLower() });
        
        if (!prologResponse.IsSuccessStatusCode)
            return BadRequest("Failed to set difficulty in Prolog service");

        // Создание сессии
        var session = new Session 
        { 
            UserId = dto.UserId,
            Difficulty = dto.Difficulty
        };

        _context.Sessions.Add(session);
        await _context.SaveChangesAsync();

        // UI должен отправляет POST-запрос на этот эндпоинт для создания сессии

        return Ok(session);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetSession(int id)
    {
        var session = await _context.Sessions
            .Include(s => s.Moves)
            .FirstOrDefaultAsync(s => s.Id == id);

        // Вызов Prolog GET /status для текущего состояния игры
        // UI должен вызывает этот эндпоинт для отображения сессии

        return session == null ? NotFound() : Ok(session);
    }

    [HttpPost("{id}/terminate")]
    public async Task<IActionResult> TerminateSession(int id)
    {
        var session = await _context.Sessions.FindAsync(id);
        if (session == null) return NotFound();

        // Prolog для получения текущих рук и очков перед завершением

        session.EndTime = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // UI должен вызывает этот эндпоинт для завершения сессии

        return NoContent();
    }
}