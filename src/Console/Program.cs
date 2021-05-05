using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using static System.Console;

namespace IntegrationEvent
{
    class Program
    {
        static IContainer container;

        static async Task Main(string[] args)
        {
            string topic = args.ElementAtOrDefault(0);
            string subscription = args.ElementAtOrDefault(1);
            string to = args.ElementAtOrDefault(2);
            string messageToSend = args.ElementAtOrDefault(3);

            if (string.IsNullOrEmpty(topic) &&
                string.IsNullOrEmpty(subscription))
            {
                ForegroundColor = ConsoleColor.Red;
                WriteLine("Invalid Topic and Subscription");
                Environment.Exit(1);
            }

            ConfigureAutofac(topic, subscription, to);

            var bus = container.ResolveOptional<IServiceBus>();
            bus.Subscribe<MessageSentEvent, MessageSentEventHandler>();

            WriteLine("Waiting for messages...");

            if (!string.IsNullOrEmpty(messageToSend))
            {
                await bus.Publish(new MessageSentEvent
                {
                    Message = messageToSend
                });
            }

            ReadLine();
        }

        static void ConfigureAutofac(string topic, string subscription, string to)
        {
            ContainerBuilder containerBuilder = new();

            containerBuilder.RegisterType<ServiceBusManager>()
                            .As<IServiceBusManager>()
                            .SingleInstance();

            containerBuilder.Register((context) =>
            {
                var busManager = context.ResolveOptional<IServiceBusManager>();
                var innerContainer = context.ResolveOptional<IContainer>();

                return new AzureServiceBus(
                    new AzureServiceBusConfig
                    {
                        ConnectionString = "<your servicebus connection string>",
                        Subscription = subscription,
                        TopicName = topic,
                        To = to
                    },
                    busManager,
                    container
                );
            })
            .As<IServiceBus>()
            .SingleInstance();

            containerBuilder.RegisterAssemblyTypes(Assembly.GetExecutingAssembly())
                            .AsClosedTypesOf(typeof(IEventHandler<>));

            container = containerBuilder.Build();
        }
    }
}