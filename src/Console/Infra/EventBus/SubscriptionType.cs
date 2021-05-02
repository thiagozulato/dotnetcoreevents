using System;

namespace IntegrationEvent
{
    public class SubscriptionType
    {
        public Type EventType { get; set; }
        public Type HandlerType { get; set; }
    }
}