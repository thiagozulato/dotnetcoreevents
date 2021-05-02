using System.Threading.Tasks;

namespace IntegrationEvent
{
    public interface IEventHandler<in T> where T : IEvent
    {
        Task Handle(T @event);
    }

}