using Rx.Net.StateMachine.Events;
using Rx.Net.StateMachine.Extensions;
using Rx.Net.StateMachine.Persistance;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Rx.Net.StateMachine.EntityFramework.Awaiters
{
    public class EventAwaiterResolver<TContext, TContextKey> : IEventAwaiterResolver
    {
        private readonly AwaitHandlerResolver<TContext, TContextKey> _awaitHandlerResolver;

        public EventAwaiterResolver(AwaitHandlerResolver<TContext, TContextKey> awaitHandlerResolver)
        {
            _awaitHandlerResolver = awaitHandlerResolver;
        }
        public IReadOnlyCollection<IEventAwaiter<TEvent>> GetEventAwaiters<TEvent>(TEvent @event)
            where TEvent : class
        {
            return GetEventAwaiters(@event, typeof(TEvent)).Cast<IEventAwaiter<TEvent>>().ToList();
        }

        public IReadOnlyCollection<IEventAwaiter> GetEventAwaiters(object @event) =>
            GetEventAwaiters(@event, @event.GetType());

        public IIgnoreSessionVersion? GetSessionVersionIgnore(object @event)
        {
            return _awaitHandlerResolver.GetAwaiterHandler(@event.GetType()).GetSessionVersionToIgnore(@event);
        }

        private IReadOnlyCollection<IEventAwaiter> GetEventAwaiters(object @event, Type eventType)
        {
            var awaiterHandler = _awaitHandlerResolver.GetAwaiterHandler(eventType);

            return awaiterHandler.GetAwaiterIdTypes()
                .Select(t => (IEventAwaiter)AwaiterExtensions.CreateAwaiter(t, @event))
                .ToList();
        }
    }
}
