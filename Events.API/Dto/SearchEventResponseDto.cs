using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Events.API.Entities;

namespace Events.API.Dto;


public record SearchEventResponseDto
{
    [Required]
    [JsonPropertyName("is_success")]
    public string IsSuccess { get; init; }

    [JsonPropertyName("results")]
    public IEnumerable<Result> Results { get; init; }
}

public record Result
{
    [JsonPropertyName("event_id")]
    public Guid EventId { get; init; }

    [JsonPropertyName("event")]
    public string Event { get; init; }

    [JsonPropertyName("parameters")]
    public Parameters Parameters { get; init; }

    [JsonPropertyName("event_datetime")]
    public DateTimeOffset EventDatetime { get; init; }
}


