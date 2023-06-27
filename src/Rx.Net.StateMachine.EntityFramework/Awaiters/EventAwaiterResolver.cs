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
        public IEnumerable<IEventAwaiter<TEvent>> GetEventAwaiters<TEvent>(TEvent @event) =>
            GetEventAwaiters(@event, typeof(TEvent)).Cast<IEventAwaiter<TEvent>>();

        public IEnumerable<IEventAwaiter> GetEventAwaiters(object @event) =>
            GetEventAwaiters(@event, @event.GetType());

        private IEnumerable<IEventAwaiter> GetEventAwaiters(object @event, Type eventType)
        {
            var awaiterHandler = _awaitHandlerResolver.GetAwaiterHandler(eventType);

            return awaiterHandler.GetAwaiterIdTypes()
                .Select(t => (IEventAwaiter)AwaiterExtensions.CreateAwaiter(t, @event))
                .ToList();
        }
    }
}
