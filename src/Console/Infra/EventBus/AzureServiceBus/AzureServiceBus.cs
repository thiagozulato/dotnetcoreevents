using System;
using System.Threading.Tasks;
using Autofac;
using Azure.Messaging.ServiceBus;
using Newtonsoft.Json;

namespace IntegrationEvent
{
    public class AzureServiceBusConfig
    {
        public string ConnectionString { get; set; }
        public string TopicName { get; set; }
        public string Subscription { get; set; }
    }

    public sealed class AzureServiceBus : IServiceBus, IDisposable
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

            Processor();
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
                To = _config.Subscription
            };

            message.ApplicationProperties.Add("eventName", @event.GetType().Name);

            await sender.SendMessageAsync(
                message
            );
        }

        public void Subscribe<T, H>()
            where T : IEvent
            where H : IEventHandler<T>
        {
            _manager.AddEvent<T, H>();
        }

        private void Processor()
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

            _processor.StartProcessingAsync().ConfigureAwait(false);
        }

        private Task ProcessError(ProcessErrorEventArgs error)
        {
            Console.WriteLine(error.Exception.Message);
            return Task.CompletedTask;
        }

        private async Task ProcessMessage(ProcessMessageEventArgs processMessage)
        {
            using var autofacScope = _autofac.BeginLifetimeScope();

            var message = processMessage.Message;
            var properties = message.ApplicationProperties;

            if (properties.TryGetValue("eventName", out var eventName))
            {
                var @event = _manager.GetEvent(Convert.ToString(eventName));
                var eventType = typeof(IEventHandler<>).MakeGenericType(@event);
                var handler = autofacScope.ResolveOptional(eventType);

                if (handler == null) return;

                var eventMessage = JsonConvert.DeserializeObject(
                    message.Body.ToString(),
                    @event
                );

                await (Task)eventType.GetMethod("Handle").Invoke(handler, new[] { eventMessage });

                await processMessage.CompleteMessageAsync(processMessage.Message);
            }
        }

        public void Dispose()
        {
            _manager.ClearAllEvents();
            _processor.StopProcessingAsync().ConfigureAwait(false);
            _processor.CloseAsync().ConfigureAwait(false);
        }
    }
}