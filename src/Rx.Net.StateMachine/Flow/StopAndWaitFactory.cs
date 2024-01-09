using Rx.Net.StateMachine.Events;
using System;
using System.Reactive.Linq;

namespace Rx.Net.StateMachine.Flow
{
    public struct StopAndWaitFactory<TSource>
    {
        private readonly IFlow<TSource> _source;
        public StopAndWaitFactory(IFlow<TSource> source)
        {
            _source = source;
        }

        /// <summary>
        /// Waits for event and than persists it
        /// </summary>
        public IFlow<TEvent> For<TEvent>(string eventStateId, IEventAwaiter<TEvent> awaiterId, Func<TEvent, bool>? filter = null)
            where TEvent : class
        {
            return _source.Select((_, scope) => scope.StopAndWait(eventStateId, awaiterId, filter));
        }

        /// <summary>
        /// Waits for event and than persists it
        /// </summary>
        public IFlow<TEvent> For<TEvent>(string eventStateId, Func<TSource, IEventAwaiter<TEvent>> getAwaiterId)
            where TEvent : class
        {
            return _source.Select((source, scope) => scope.StopAndWait(eventStateId, getAwaiterId(source), null));
        }

        /// <summary>
        /// Waits for event and than persists it
        /// </summary>
        public IFlow<TEvent> For<TEvent>(string eventStateId, Func<TSource, StateMachineScope, IEventAwaiter<TEvent>> getAwaiterId)
            where TEvent : class
        {
            return _source.Select((source, scope) => scope.StopAndWait(eventStateId, getAwaiterId(source, scope), null));
        }

        /// <summary>
        /// Waits for event and than persists it
        /// </summary>
        public IFlow<TEvent> For<TEvent>(string eventStateId, Func<TSource, IEventAwaiter<TEvent>> getAwaiterId, Func<TEvent, bool>? filter = null)
            where TEvent : class
        {
            return _source.Select((source, scope) => scope.StopAndWait(eventStateId, getAwaiterId(source), filter));
        }

        /// <summary>
        /// Waits for event and than persists it
        /// </summary>
        public IFlow<TEvent> For<TEvent>(string eventStateId, IEventAwaiter<TEvent> awaiterId, Func<TEvent, TSource, bool> filter)
            where TEvent : class
        {
            if (filter == null)
                throw new ArgumentNullException(nameof(filter));

            return _source.Select((s, scope) =>
            {
                return scope.StopAndWait(eventStateId, awaiterId, e => filter(e, s));
            });
        }
    }
}
