using Rx.Net.StateMachine.Events;
using Rx.Net.StateMachine.Extensions;
using Rx.Net.StateMachine.Persistance;
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
        public IEnumerable<IEventAwaiter<TEvent>> GetEventAwaiters<TEvent>(TEvent @event)
        {
            var awaiterHandler = _awaitHandlerResolver.GetAwaiterHandler(typeof(TEvent));

            return awaiterHandler.GetAwaiterIdTypes()
                .Select(t => (IEventAwaiter<TEvent>) AwaiterExtensions.CreateAwaiter(t, @event))
                .ToList();
        }
    }
}
