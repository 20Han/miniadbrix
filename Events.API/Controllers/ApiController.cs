using Events.API.Dto;
using Events.API.Entities;
using Events.API.Repository;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Events.API.Controllers;

[ApiController]
[Route("[Controller]")]
public class ApiController : ControllerBase
{
    private readonly IEventRepository repository;
    

    public ApiController(
        IEventRepository repository
    )
    {
        this.repository = repository;
    }

    [HttpPost]
    [Route("save")]
    public async Task<CollectEventResponseDto> SaveItem(CollectEventDto collectEventDto)
    {
        Event _event = new()
        {
            EventId = collectEventDto.EventId,
            UserId = collectEventDto.UserId,
            EventName = collectEventDto.EventName,
            Parameters = collectEventDto.Parameters,
            CreateDate = DateTimeOffset.UtcNow
        };

        bool isSuccess = await repository.SaveEvent(_event);

        return new CollectEventResponseDto
        {
            IsSuccess = isSuccess ? "true" : "false"
        };
    }

    [HttpPost]
    [Route("collect")]
    public async Task<CollectEventResponseDto> CollectItem(CollectEventDto collectEventDto)
    {
        Event _event = new()
        {
            EventId = collectEventDto.EventId,
            UserId = collectEventDto.UserId, 
            EventName = collectEventDto.EventName,
            Parameters = collectEventDto.Parameters,
            CreateDate = DateTimeOffset.UtcNow
        };

        string message = JsonConvert.SerializeObject(_event);
        string queueName = "EventsQueue.fifo";

        bool isSuccess = await repository.SendSQS(queueName, message);

        return new CollectEventResponseDto
        {
            IsSuccess = isSuccess ? "true" : "false"
        };
    }

    [HttpPost]
    [Route("search")]
    public async Task<SearchEventResponseDto> SearchItem(SearchEventDto searchEventDto)
    {
        var results = (await repository.GetEvent(searchEventDto.UserId))
            .Select(_event => new Result
            {
                EventId = _event.EventId,
                Event = _event.EventName,
                Parameters = _event.Parameters,
                EventDatetime = _event.CreateDate
            });


        return new SearchEventResponseDto
        {
            IsSuccess = "true",
            Results = results
        };
    }
}