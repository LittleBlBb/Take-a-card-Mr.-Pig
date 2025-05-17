using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CardGameAPI.Data;
using Microsoft.EntityFrameworkCore;

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
        var user = await _context.Users
            .Where(u => u.Id == id)
            .Select(u => new { u.Id, u.Username })
            .FirstOrDefaultAsync();

        return user == null ? NotFound() : Ok(user);


    // необходимо добавить эндпоинт GET /users/{id}/stats для статистики (FR3.2)
    // необходимо добавить эндпоинт GET /users/{id}/avg_moves для среднего количества ходов (FR3.2)
    // UI вызывает GET /users/{id} для отображения профиля
    }
}