using Rx.Net.StateMachine.EntityFramework.Tables;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Rx.Net.StateMachine.EntityFramework.Awaiters
{
    public class DefaultAwaiterHandler<TContext, TContextKey, TEvent> : IAwaiterHandler<TContext, TContextKey, TEvent>
    {
        private static readonly Expression<Func<SessionStateTable<TContext, TContextKey>, bool>> DefaultFilter = ss => true;
        private Func<TEvent, Expression<Func<SessionStateTable<TContext, TContextKey>, bool>>>? SessionStateFilter { get; }
        private readonly IReadOnlyCollection<Type> _awaiterIdTypes;

        public DefaultAwaiterHandler(Func<TEvent, Expression<Func<SessionStateTable<TContext, TContextKey>, bool>>>? sessionStateFilter, IReadOnlyCollection<Type> awaiterIdTypes)
        {
            SessionStateFilter = sessionStateFilter;
            _awaiterIdTypes = awaiterIdTypes;
        }

        public Expression<Func<SessionStateTable<TContext, TContextKey>, bool>> GetSessionStateFilter(TEvent ev) =>
            SessionStateFilter == null ? DefaultFilter : SessionStateFilter(ev);

        public IEnumerable<Type> GetAwaiterIdTypes() => _awaiterIdTypes;

        public Expression<Func<SessionStateTable<TContext, TContextKey>, bool>> GetSessionStateFilter(object ev)
        {
            return GetSessionStateFilter((TEvent)ev);
        }
    }
}
