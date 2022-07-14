using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Events.API.Entities;

namespace Events.API.Dto;

public record CollectEventDto
{
    [Required]
    [JsonPropertyName("event_id")]
    public Guid EventId { get; init; }

    [Required]
    [JsonPropertyName("user_id")]
    public Guid UserId { get; init; }

    [Required]
    [JsonPropertyName("event")]
    public string? EventName { get; init; }

    [Required]
    [JsonPropertyName("parameters")]
    public Parameters? Parameters { get; init; }
}

