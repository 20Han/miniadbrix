using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace Events.API.Dto;

public record CollectEventResponseDto
{
    [Required]
    [JsonPropertyName("is_success")]
    public string IsSuccess { get; init; }
}


