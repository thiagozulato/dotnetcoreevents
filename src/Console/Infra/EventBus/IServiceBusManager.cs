using System;

namespace IntegrationEvent
{
    public interface IServiceBusManager
    {
        void AddEvent<T, H>()
            where T : IEvent
            where H : IEventHandler<T>;

        bool HasEvent(string eventName);
        Type GetEvent(string eventName);

        void ClearAllEvents();
    }
}