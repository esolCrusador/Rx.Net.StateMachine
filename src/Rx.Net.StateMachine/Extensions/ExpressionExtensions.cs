using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Rx.Net.StateMachine.Extensions
{
    public static class ExpressionExtensions
    {
        public static Expression<Func<TSource, TResult>> AggregateExpression<TSource, TResult>(
            Expression<Func<TResult, TResult, TResult>> resultSelector,
            params Expression<Func<TSource, TResult>>[] expressions)
        {
            return AggregateExpression(expressions, resultSelector);
        }

        public static Expression<Func<TSource, TResult>> AggregateExpression<TSource, TResult>(
            this IReadOnlyList<Expression<Func<TSource, TResult>>> expressions,
            Expression<Func<TResult, TResult, TResult>> resultSelector)
        {
            if (expressions.Count < 2)
                return expressions[0];

            return expressions.Aggregate((Expression<Func<TSource, TResult>>?)null,
                (accumulate, expression) =>
                {
                    if (accumulate == null)
                        return expression;

                    var rebinder = new ExpressionRebinder(new Dictionary<Expression, Expression>
                    {
                        [resultSelector.Parameters[0]] = accumulate.Body,
                        [resultSelector.Parameters[1]] = expression.Body,
                        [expression.Parameters[0]] = accumulate.Parameters[0]
                    });

                    var expressionBody = rebinder.Visit(resultSelector.Body);
                    return Expression.Lambda<Func<TSource, TResult>>(
                        rebinder.Visit(expressionBody)!,
                        accumulate.Parameters[0]
                    );
                }
            )!;
        }
    }
}
