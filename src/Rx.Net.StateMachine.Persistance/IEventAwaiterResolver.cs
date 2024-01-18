using Rx.Net.StateMachine.Events;
using System;
using System.Collections.Generic;

namespace Rx.Net.StateMachine.Persistance
{
    public interface IEventAwaiterResolver
    {
        public IReadOnlyCollection<IEventAwaiter<TEvent>> GetEventAwaiters<TEvent>(TEvent @event)
            where TEvent: class;
        public IReadOnlyCollection<IEventAwaiter> GetEventAwaiters(object @event);
        public IIgnoreSessionVersion? GetSessionVersionIgnore(object @event);
    }
}
