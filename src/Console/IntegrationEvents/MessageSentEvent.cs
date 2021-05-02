namespace IntegrationEvent
{
    public class MessageSentEvent : IEvent
    {
        public string Message { get; set; }
    }
}