using Events.API.Entities;

namespace Events.API.Repository;

public interface IEventRepository
{
    Task<bool> SendSQS(string queueName,string message);
    Task<bool> SaveEvent(Event _event);
    Task<IEnumerable<Event>> GetEvent(Guid userId);
}

