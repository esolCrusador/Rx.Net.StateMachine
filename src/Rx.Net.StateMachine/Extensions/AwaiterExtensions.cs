using Rx.Net.StateMachine.Events;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Rx.Net.StateMachine.Extensions
{
    public static class AwaiterExtensions
    {
        private static readonly ParameterExpression EventParameter = Expression.Parameter(typeof(object), "@event");
        private static readonly Dictionary<Type, Func<object, IEventAwaiter>> _eventAwaiterFactories = new Dictionary<Type, Func<object, IEventAwaiter>>();
        public static IEventAwaiter CreateAwaiter(Type awaiterType, object @event)
        {
            var awaiterFactory = GetAwaiterFactory(awaiterType, @event.GetType());
            return awaiterFactory(@event);
        }

        private static Func<object, IEventAwaiter> GetAwaiterFactory(Type awaiterType, Type eventType)
        {
            if (!_eventAwaiterFactories.TryGetValue(awaiterType, out var factory))
                lock (_eventAwaiterFactories)
                    if (!_eventAwaiterFactories.TryGetValue(awaiterType, out factory))
                        _eventAwaiterFactories.Add(awaiterType, factory = CreateAwaiterFactory(awaiterType, eventType));

            return factory;
        }

        private static Func<object, IEventAwaiter> CreateAwaiterFactory(Type awaiterType, Type eventType)
        {
            var eventConstructor = awaiterType.GetConstructor(new Type[] { eventType });

            Expression body;
            if (eventConstructor != null)
                body = Expression.New(eventConstructor, Expression.Convert(EventParameter, eventType));
            else
                body = Expression.New(
                    awaiterType.GetConstructor(new Type[0]) ?? throw new ArgumentException($"Awaiter {awaiterType.FullName} must have default constructor or constructor accepting ({eventType.FullName})")
                );

            return Expression.Lambda<Func<object, IEventAwaiter>>(body, EventParameter).Compile();
        }
    }
}
