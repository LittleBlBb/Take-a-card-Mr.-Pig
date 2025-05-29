using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using CardGameAPI.Models;

namespace CardGameAPI.Services;

public class PrologService
{
    private readonly HttpClient _prologClient;

    public PrologService(IHttpClientFactory clientFactory)
    {
        _prologClient = clientFactory.CreateClient("Prolog");
    }

    public async Task<bool> InitializeGameAsync()
    {
        var response = await _prologClient.PostAsync("init", null);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Prolog init failed: {response.StatusCode}");
        }
        return true;
    }

    public async Task<bool> SetDifficultyAsync(int difficulty)
    {
        var difficultyName = difficulty switch
        {
            0 => "easy",
            1 => "medium",
            2 => "hard",
            3 => "expert",
            _ => "medium"
        };

        var response = await _prologClient.PostAsJsonAsync("set_difficulty", new { difficulty = difficultyName });
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Prolog set_difficulty failed: {response.StatusCode}");
        }
        return true;
    }

    public async Task<PrologMoveResponse> ProcessMoveAsync(string rank, bool enableMemory)
    {
        var response = await _prologClient.PostAsJsonAsync("move", new { rank, enable_memory = enableMemory });
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Prolog move failed: {response.StatusCode}, {errorContent}");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var result = json.GetProperty("result").GetString();
        var playerScore = json.GetProperty("player_score").GetInt32();
        var botScore = json.GetProperty("bot_score").GetInt32();
        var playerHand = json.GetProperty("player_hand").EnumerateArray()
            .Select(card => new Card { Rank = card.GetProperty("rank").GetString(), Suit = card.GetProperty("suit").GetString() })
            .ToList();
        var gameState = json.GetProperty("game_state").GetString();
        var winner = json.TryGetProperty("winner", out var winnerProp) && winnerProp.ValueKind != JsonValueKind.Null
            ? winnerProp.GetString()
            : null;

        List<Card>? botHand = null;
        if (json.TryGetProperty("bot_hand", out var botHandProp) && botHandProp.ValueKind == JsonValueKind.Array)
        {
            botHand = botHandProp.EnumerateArray()
                .Select(card => new Card { Rank = card.GetProperty("rank").GetString(), Suit = card.GetProperty("suit").GetString() })
                .ToList();
        }

        return new PrologMoveResponse
        {
            Result = result,
            PlayerScore = playerScore,
            BotScore = botScore,
            PlayerHand = playerHand,
            BotHand = botHand,
            GameState = gameState,
            Winner = winner
        };
    }

    public async Task<PrologStatusResponse> GetGameStatusAsync()
    {
        var response = await _prologClient.GetAsync("status");
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Prolog status failed: {response.StatusCode}");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var playerScore = json.GetProperty("player_score").GetInt32();
        var botScore = json.GetProperty("bot_score").GetInt32();
        var winner = json.TryGetProperty("winner", out var winnerProp) && winnerProp.ValueKind != JsonValueKind.Null
            ? winnerProp.GetString()
            : null;

        return new PrologStatusResponse
        {
            PlayerScore = playerScore,
            BotScore = botScore,
            Winner = winner
        };
    }
}

public class PrologMoveResponse
{
    public string? Result { get; set; }
    public int PlayerScore { get; set; }
    public int BotScore { get; set; }
    public List<Card>? PlayerHand { get; set; }
    public List<Card>? BotHand { get; set; }
    public string? GameState { get; set; }
    public string? Winner { get; set; }
}

public class PrologStatusResponse
{
    public int PlayerScore { get; set; }
    public int BotScore { get; set; }
    public string? Winner { get; set; }
}