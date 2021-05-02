using System;
using System.Collections.Generic;

namespace IntegrationEvent
{
    public class ServiceBusManager : IServiceBusManager
    {
        private readonly Dictionary<string, SubscriptionType> _events = new();

        public void AddEvent<T, H>()
            where T : IEvent
            where H : IEventHandler<T>
        {
            string eventName = typeof(T).Name;

            if (_events.ContainsKey(eventName))
            {
                return;
            }

            _events.Add(eventName, new SubscriptionType
            {
                EventType = typeof(T),
                HandlerType = typeof(H),
            });
        }

        public bool HasEvent(string eventName)
        {
            return _events.ContainsKey(eventName);
        }

        public Type GetEvent(string eventName)
        {
            return _events[eventName].EventType;
        }

        public void ClearAllEvents()
        {
            _events.Clear();
        }
    }
}