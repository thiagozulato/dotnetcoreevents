using System.Threading.Tasks;

namespace IntegrationEvent
{
    public interface IServiceBus
    {
        Task Publish<T>(T @event) where T : IEvent;
        void Subscribe<T, H>()
            where T : IEvent
            where H : IEventHandler<T>;
    }
}