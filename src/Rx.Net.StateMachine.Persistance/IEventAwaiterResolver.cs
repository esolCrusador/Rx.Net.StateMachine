using Rx.Net.StateMachine.Events;
using System.Collections.Generic;

namespace Rx.Net.StateMachine.Persistance
{
    public interface IEventAwaiterResolver
    {
        public IEnumerable<IEventAwaiter<TEvent>> GetEventAwaiters<TEvent>(TEvent @event);
        public IEnumerable<IEventAwaiter> GetEventAwaiters(object @event);
    }
}
