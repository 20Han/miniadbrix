using Amazon.SQS;
using Amazon.SQS.Model;
using Events.API.DB;
using Events.API.Entities;
using Newtonsoft.Json;

namespace Events.API.Repository;

public class EventRepository : IEventRepository
{
    private readonly EventDBContext dbContext;
    private readonly IAmazonSQS sqs;

    public EventRepository
    (
        EventDBContext dbContext,
        IAmazonSQS sqs
    )
    {
        this.dbContext = dbContext;
        this.sqs = sqs;
    }

    public async Task<IEnumerable<Event>> GetEvent(Guid userId)
    {
        return await Task.FromResult(dbContext.Events.Where(e => e.UserId == userId));
    }

    public async Task<bool> SaveEvent(Event _event)
    {
        if (dbContext.Events.Any(x => x.EventId == _event.EventId ))
        {
            return await Task.FromResult(false);
        }

        dbContext.Events.Add(_event);
        dbContext.SaveChanges();
        return await Task.FromResult(true);
    }

    public async Task<bool> SendSQS(string queueName, string message)
    {
        var queueUrl = await sqs.GetQueueUrlAsync(queueName);

        var request = new SendMessageRequest
        {
            QueueUrl = queueUrl.QueueUrl,
            MessageBody = message,
        };

        SendMessageResponse responseSendMsg = await sqs.SendMessageAsync(request);

        bool isSuccess = responseSendMsg.HttpStatusCode == System.Net.HttpStatusCode.OK;

        Console.WriteLine($"SendSQS :: HttpStatusCode: {responseSendMsg.HttpStatusCode}");

        return isSuccess;
    }
}

