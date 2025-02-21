using Rx.Net.StateMachine.EntityFramework.Tables;
using Rx.Net.StateMachine.Events;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Rx.Net.StateMachine.EntityFramework.Awaiters
{
    public interface IAwaiterHandler<TContext, TContextKey>
    {
        public Expression<Func<SessionStateTable<TContext, TContextKey>, bool>> GetSessionStateFilter(object ev);
        public IEnumerable<Type> GetAwaiterIdTypes();
        public IStaleSessionVersion? GetStaleSessionVersion(object ev);
    }
    public interface IAwaiterHandler<TContext, TContextKey, TEvent> : IAwaiterHandler<TContext, TContextKey>
    {
        public Expression<Func<SessionStateTable<TContext, TContextKey>, bool>> GetSessionStateFilter(TEvent ev);
    }
}
