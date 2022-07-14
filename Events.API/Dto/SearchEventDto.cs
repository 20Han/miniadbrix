using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

public record SearchEventDto
{
    [Required]
    [JsonPropertyName("user_id")]
    public Guid UserId { get; init; }
}

