using System.ComponentModel.DataAnnotations;

namespace BazingaGame.Models;

public record PlayRequest([Range(1, 5)] int Player);
