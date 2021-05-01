using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Autofac;
using static System.Console;

namespace IntegrationEvent
{
    class Program
    {
        static readonly ContainerBuilder containerBuilder = new ContainerBuilder();
        static IContainer container;

        static void Main(string[] args)
        {
            containerBuilder.RegisterType<ServiceBusManager>().As<IServiceBusManager>().SingleInstance();

            containerBuilder.Register((context) =>
            {
                var busManager = context.ResolveOptional<IServiceBusManager>();
                return new ServiceBus(busManager, container);
            }).As<IServiceBus>().SingleInstance();

            containerBuilder.RegisterAssemblyTypes(Assembly.GetExecutingAssembly())
                            .AsClosedTypesOf(typeof(IEventHandler<>));

            container = containerBuilder.Build();

            var bus = container.ResolveOptional<IServiceBus>();
            bus.Subscribe<UsuarioCadastradoEvent, UsuarioCadastradoEventHandler>();
            bus.Subscribe<UsuarioExcluidoEvent, UsuarioCadastradoEventHandler>();

            bus.Publish(new UsuarioCadastradoEvent
            {
                Nome = "User Name"
            });

            bus.Publish(new UsuarioExcluidoEvent
            {
                UsuarioId = 1322
            });

            ReadLine();
        }
    }

    public class UsuarioCadastradoEvent : IEvent
    {
        public string Nome { get; set; }
    }

    public class UsuarioExcluidoEvent : IEvent
    {
        public int UsuarioId { get; set; }
    }

    public class UsuarioCadastradoEventHandler :
        IEventHandler<UsuarioCadastradoEvent>,
        IEventHandler<UsuarioExcluidoEvent>
    {
        public Task Handle(UsuarioCadastradoEvent @event)
        {
            WriteLine($"Evento recebido {System.Text.Json.JsonSerializer.Serialize(@event)}");
            return Task.CompletedTask;
        }

        public Task Handle(UsuarioExcluidoEvent @event)
        {
            WriteLine($"Usuario com o Id {@event.UsuarioId} foi excluído");
            return Task.CompletedTask;
        }
    }

    public interface IServiceBus
    {
        Task Publish<T>(T @event) where T : IEvent;
        void Subscribe<T, H>()
            where T : IEvent
            where H : IEventHandler<T>;
    }

    public class ServiceBus : IServiceBus
    {
        readonly IServiceBusManager _manager;
        readonly IContainer _autofac;
        public ServiceBus(IServiceBusManager manager, IContainer autofac)
        {
            _manager = manager;
            _autofac = autofac;
        }

        public async Task Publish<T>(T @event) where T : IEvent
        {
            if (!_manager.HasEvent(typeof(T).Name))
            {
                return;
            }

            var handler = _autofac.ResolveOptional<IEventHandler<T>>();

            if (handler == null) return;

            await handler.Handle(@event);
        }

        public void Subscribe<T, H>()
            where T : IEvent
            where H : IEventHandler<T>
        {
            _manager.AddEvent<T, H>();
        }
    }

    public interface IEvent
    {

    }

    public interface IEventHandler<in T> where T : IEvent
    {
        Task Handle(T @event);
    }

    public interface IServiceBusManager
    {
        void AddEvent<T, H>();

        bool HasEvent(string eventName);
        Type GetEvent(string eventName);
    }

    public class ServiceBusManager : IServiceBusManager
    {
        private readonly Dictionary<string, Type> _events = new();

        public void AddEvent<T, H>()
        {
            string eventName = typeof(T).Name;

            if (_events.ContainsKey(eventName))
            {
                return;
            }

            _events.Add(eventName, typeof(H));
        }

        public bool HasEvent(string eventName)
        {
            return _events.ContainsKey(eventName);
        }

        public Type GetEvent(string eventName)
        {
            return _events[eventName];
        }
    }
}