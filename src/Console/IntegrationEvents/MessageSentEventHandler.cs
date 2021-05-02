using System.Threading.Tasks;
using static System.Console;

namespace IntegrationEvent
{
    public class MessageSentEventHandler : IEventHandler<MessageSentEvent>
    {
        public Task Handle(MessageSentEvent @event)
        {
            WriteLine($"Event received {System.Text.Json.JsonSerializer.Serialize(@event)}");
            return Task.CompletedTask;
        }
    }
}