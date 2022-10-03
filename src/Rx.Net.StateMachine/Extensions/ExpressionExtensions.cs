using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Rx.Net.StateMachine.Extensions
{
    public static class ExpressionExtensions
    {
        public static Expression<Func<TSource, TResult>> Aggregate<TSource, TResult>(
            Expression<Func<TResult, TResult, TResult>> resultSelector,
            params Expression<Func<TSource, TResult>>[] expressions)
        {
            if (expressions.Length < 2)
                throw new ArgumentException("Please provide at least 2 expressions", nameof(expressions));

            return expressions.Aggregate((Expression<Func<TSource, TResult>>)null,
                (accumulate, expression) =>
                {
                    if (accumulate == null)
                        return expression;

                    var rebinder = new ExpressionRebinder(new Dictionary<Expression, Expression>
                    {
                        [resultSelector.Parameters[0]] = accumulate.Body,
                        [resultSelector.Parameters[1]] = expression,
                        [expression.Parameters[0]] = accumulate.Parameters[0]
                    });

                    var expressionBody = rebinder.Visit(expression.Body);
                    return Expression.Lambda<Func<TSource, TResult>>(
                        rebinder.Visit(accumulate.Body),
                        accumulate.Parameters[0]
                    );
                }
            );
        }
    }
}
