using System;
using System.Threading.Tasks;
using Autofac;
using Azure.Messaging.ServiceBus;
using Newtonsoft.Json;

namespace IntegrationEvent
{
    // TODO: Put this information in a configuration file like appsettings
    public class AzureServiceBusConfig
    {
        public string ConnectionString { get; set; }
        public string TopicName { get; set; }
        public string Subscription { get; set; }
        public string To { get; set; }
    }

    public sealed class AzureServiceBus : IServiceBus, IAsyncDisposable
    {
        readonly AzureServiceBusConfig _config;
        readonly IContainer _autofac;
        readonly IServiceBusManager _manager;
        ServiceBusClient _client;
        ServiceBusProcessor _processor;

        public AzureServiceBus(
            AzureServiceBusConfig config,
            IServiceBusManager manager,
            IContainer autofac
        )
        {
            _config = config;
            _autofac = autofac;
            _manager = manager;

            Processor().GetAwaiter().GetResult();
        }

        private ServiceBusClient Client
        {
            get
            {
                if (_client == null || _client.IsClosed)
                {
                    _client = new ServiceBusClient(_config.ConnectionString);
                }

                return _client;
            }
        }

        public async Task Publish<T>(T @event) where T : IEvent
        {
            await using ServiceBusSender sender = Client.CreateSender(_config.TopicName);

            var message = new ServiceBusMessage
            {
                Body = BinaryData.FromString(JsonConvert.SerializeObject(@event)),
                CorrelationId = Guid.NewGuid().ToString(),
                MessageId = Guid.NewGuid().ToString(),
                Subject = @event.GetType().Name,
                // This property is used to filter the messages. It's like a routing key. This filter can be created in the Azure Portal.
                // If no filter is provided for the subscription then it will receive all messages from the topic.
                To = _config.To
            };

            // This can be used to pass information about the HTTP Request. This may have an additional parameter to provide extra properties.
            //message.ApplicationProperties.Add("eventName", @event.GetType().Name);

            await sender.SendMessageAsync(message);
        }

        public void Subscribe<T, H>()
            where T : IEvent
            where H : IEventHandler<T>
        {
            _manager.AddEvent<T, H>();
        }

        private async Task Processor()
        {
            _processor = Client.CreateProcessor(
                _config.TopicName,
                _config.Subscription,
                new ServiceBusProcessorOptions
                {
                    AutoCompleteMessages = false,
                    MaxConcurrentCalls = 10,
                    ReceiveMode = ServiceBusReceiveMode.PeekLock,
                }
            );

            _processor.ProcessMessageAsync += ProcessMessage;
            _processor.ProcessErrorAsync += ProcessError;

            await _processor.StartProcessingAsync();
        }

        private Task ProcessError(ProcessErrorEventArgs error)
        {
            // TODO: Use an interface like ILogger to log the errors to an external tool.
            Console.WriteLine(error.Exception.Message);
            return Task.CompletedTask;
        }

        private async Task ProcessMessage(ProcessMessageEventArgs processMessage)
        {
            using var autofacScope = _autofac.BeginLifetimeScope();

            var message = processMessage.Message;
            var properties = message.ApplicationProperties;

            if (_manager.HasEvent(message.Subject))
            {
                var @event = _manager.GetEvent(message.Subject);
                var eventType = typeof(IEventHandler<>).MakeGenericType(@event);
                var handler = autofacScope.ResolveOptional(eventType);

                if (handler == null) return;

                var eventMessage = JsonConvert.DeserializeObject(
                    message.Body.ToString(),
                    @event
                );

                await (Task)eventType.GetMethod("Handle").Invoke(handler, new[] { eventMessage });

                await processMessage.CompleteMessageAsync(message);
            }
        }

        public async ValueTask DisposeAsync()
        {
            _manager.ClearAllEvents();
            await _processor.StopProcessingAsync();
            await _processor.CloseAsync();
            await Client.DisposeAsync();
        }
    }
}