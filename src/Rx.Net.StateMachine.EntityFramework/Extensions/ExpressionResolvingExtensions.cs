using Rx.Net.StateMachine.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Rx.Net.StateMachine.EntityFramework.Extensions
{
    internal static class ExpressionResolvingExtensions
    {
        /// <summary>
        /// This method is Expression injection point. When ApplyExpressions() is called expression placeholder is replaced with expression body.
        /// </summary>
        /// <typeparam name="TSource">Source param type.</typeparam>
        /// <typeparam name="TDest">Destanation param type.</typeparam>
        /// <param name="expression">Target expression.</param>
        /// <param name="source">Expression parameter.</param>
        /// <returns>Default destanation.</returns>
        public static TDest Invoke<TSource, TDest>(this Expression<Func<TSource, TDest>> expression, TSource source)
        {
            return expression.Compile()(source);
        }

        /// <summary>
        /// This method is Expression injection point for Enumerable. When ApplyExpressions() is called expression placeholder is replaced with expression body.
        /// </summary>
        /// <typeparam name="TSource">Source param type.</typeparam>
        /// <typeparam name="TDest">Destanation param type.</typeparam>
        /// <typeparam name="TSourceEnumerable">Enumerable child type.</typeparam>
        /// <param name="expression">Target expression.</param>
        /// <param name="source">Expression parameter.</param>
        /// <returns>Default destanation enumerable.</returns>
        public static IEnumerable<TDest> InvokeEnumerable<TSource, TDest, TSourceEnumerable>(this Expression<Func<TSource, TDest>> expression, TSourceEnumerable source)
            where TSourceEnumerable : IEnumerable<TSource>
        {
            var func = expression.Compile();

            return source.Select(func);
        }

        /// <summary>
        /// Replaces Invoke(), InvokeEnumerable() injection points with expressions bodies.
        /// </summary>
        /// <typeparam name="TSource">Source expression param type.</typeparam>
        /// <typeparam name="TDest">Destanation expression param type.</typeparam>
        /// <param name="expression">Target expression.</param>
        /// <returns>Expression with replaced placeholders.</returns>
        public static Expression<Func<TSource, TDest>> ApplyExpressions<TSource, TDest>(this Expression<Func<TSource, TDest>> expression)
        {
            ResolveExpressionRebinder rebiner = new ResolveExpressionRebinder();

            return Expression.Lambda<Func<TSource, TDest>>(rebiner.Visit(expression.Body), expression.Parameters[0]);
        }

        public static Expression Continue(this Expression sourceBody, Expression continueBody, ParameterExpression continueParameter)
        {
            var rebinder = new ExpressionRebinder(continueParameter, sourceBody);

            return rebinder.Visit(continueBody) ?? throw new ArgumentException("Invalid rebinder");
        }

        public static Expression Continue(this Expression sourceBody, LambdaExpression continueExpression)
        {
            return Continue(sourceBody, continueExpression.Body, continueExpression.Parameters[0]);
        }
    }
}
