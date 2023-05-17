using System;
using System.Collections.Generic;
using System.Linq;

namespace Rx.Net.StateMachine.EntityFramework.Awaiters
{
    public class AwaitHandlerResolver<TContext, TContextKey>
    {
        private readonly Dictionary<Type, IAwaiterHandler<TContext, TContextKey>> _handlers;

        public AwaitHandlerResolver(IEnumerable<IAwaiterHandler<TContext, TContextKey>> handlers)
        {
            var handlersList = handlers.ToList();
            var awaiterTypes = handlersList.SelectMany(aw => aw.GetAwaiterIdTypes()).ToList();
            if (awaiterTypes.Distinct().Count() != awaiterTypes.Count)
                throw new ArgumentException($"Awaiter is registered more than once {string.Join(", ", awaiterTypes.GroupBy(g => g).Where(at => at.Count() > 1).Select(g => g.Key))}");

            _handlers = handlersList.ToDictionary(h =>
            {
                var genericInterface = h.GetType().GetInterface($"{nameof(IAwaiterHandler<TContext, TContextKey>)}`3");
                if (genericInterface == null)
                    throw new ArgumentException($"Handler {h.GetType().FullName} must implement {nameof(IAwaiterHandler<TContext, TContextKey>)}`` with 3 generic arguments");

                var eventType = genericInterface.GenericTypeArguments[2];
                return eventType;
            });
        }

        public IAwaiterHandler<TContext, TContextKey> GetAwaiterHandler(Type eventType)
        {
            var handler = _handlers.GetValueOrDefault(eventType)
                ?? throw new ArgumentException($"Event awaiter handler for {eventType.FullName} was not added");

            return (IAwaiterHandler<TContext, TContextKey>)handler;
        }
    }
}
