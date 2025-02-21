using Rx.Net.StateMachine.EntityFramework.Tables;
using Rx.Net.StateMachine.Events;
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
        private readonly Func<object, IStaleSessionVersion>? _getSessionVersionToIgnore;

        public DefaultAwaiterHandler(
            Func<TEvent, Expression<Func<SessionStateTable<TContext, TContextKey>, bool>>>? sessionStateFilter,
            IReadOnlyCollection<Type> awaiterIdTypes,
            Func<TEvent, IStaleSessionVersion>? getSessionVersionToIgnore
        )
        {
            SessionStateFilter = sessionStateFilter;
            _awaiterIdTypes = awaiterIdTypes;
            _getSessionVersionToIgnore = getSessionVersionToIgnore == null ? null : ev => getSessionVersionToIgnore((TEvent)ev);
        }

        public Expression<Func<SessionStateTable<TContext, TContextKey>, bool>> GetSessionStateFilter(TEvent ev) =>
            SessionStateFilter == null ? DefaultFilter : SessionStateFilter(ev);

        public IEnumerable<Type> GetAwaiterIdTypes() => _awaiterIdTypes;

        public Expression<Func<SessionStateTable<TContext, TContextKey>, bool>> GetSessionStateFilter(object ev) =>
            GetSessionStateFilter((TEvent)ev);

        public IStaleSessionVersion? GetStaleSessionVersion(object ev) =>
            _getSessionVersionToIgnore?.Invoke(ev);
    }
}
