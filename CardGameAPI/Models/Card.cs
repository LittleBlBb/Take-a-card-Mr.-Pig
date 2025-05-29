using System.ComponentModel.DataAnnotations;

namespace CardGameAPI.Models;

public class Card
{
    public string? Rank { get; set; }
    public string? Suit { get; set; }
}