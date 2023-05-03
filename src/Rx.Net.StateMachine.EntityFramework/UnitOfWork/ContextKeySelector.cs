using System;
using System.Linq.Expressions;

namespace Rx.Net.StateMachine.EntityFramework.UnitOfWork
{
    public class ContextKeySelector<TContext, TContextKey>
    {
        public Func<TContext, TContextKey> GetContextKey { get; }
        public Expression<Func<TContext, TContextKey>> GetContextKeyExpression { get; }

        public ContextKeySelector(Expression<Func<TContext, TContextKey>> expression)
        {
            GetContextKey = expression.Compile();
            GetContextKeyExpression = expression;
        }
    }
}
