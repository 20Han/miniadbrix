using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EventsWorker.Lambda;

public record Event
{
    public Guid EventId { get; init; }
    public Guid UserId { get; init; }

    public string? EventName { get; init; }
    public Parameters? Parameters { get; init; }
    public DateTimeOffset CreateDate { get; set; }
}

public record Parameters
{
    [Required]
    [JsonPropertyName("order_id")]
    [Key]
    public Guid OrderId { get; init; }

    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    [JsonPropertyName("price")]
    public int Price { get; init; }
}


