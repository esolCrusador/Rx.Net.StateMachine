using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rx.Net.StateMachine.EntityFramework.Awaiters;
using Rx.Net.StateMachine.EntityFramework.ContextDfinition;
using Rx.Net.StateMachine.EntityFramework.Tables;
using Rx.Net.StateMachine.EntityFramework.Tests.UnitOfWork;
using Rx.Net.StateMachine.EntityFramework.UnitOfWork;
using Rx.Net.StateMachine.Events;
using Rx.Net.StateMachine.Persistance;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Rx.Net.StateMachine.EntityFramework
{
    public static class EFStateMachineBootstrapper
    {
        public static ContextBuilder AddEFStateMachine(this IServiceCollection services)
        {
            return new ContextBuilder(services);
        }
        private static IServiceCollection AddEFStateMachine<TUnitOfWork, TDbContext, TContext, TContextKey>(
            this IServiceCollection services,
            Func<IServiceProvider, TDbContext> createDbContext,
            ContextKeySelector<TContext, TContextKey> contextKeySelector
        )
            where TDbContext : DbContext
            where TUnitOfWork : EFSessionStateUnitOfWork<TContext, TContextKey>, new()
            where TContext : class
        {
            services.AddSingleton(
                sp => new SessionStateDbContextFactory<TDbContext>(() => createDbContext(sp))
            );
            services.AddSingleton(contextKeySelector);
            services.AddSingleton<SessionStateDbContextFactory>(
                sp => sp.GetRequiredService<SessionStateDbContextFactory<TDbContext>>()
            );
            services.AddSingleton<ISessionStateUnitOfWorkFactory, EFSessionStateUnitOfWorkFactory<TContext, TContextKey, TUnitOfWork>>();
            services.AddSingleton<AwaitHandlerResolver<TContext, TContextKey>>();
            services.AddSingleton<IEventAwaiterResolver, EventAwaiterResolver<TContext, TContextKey>>();

            return services;
        }

        public struct ContextBuilder
        {
            private readonly IServiceCollection _services;

            public ContextBuilder(IServiceCollection services)
            {
                _services = services;
            }

            public UserContextBuilder<TContext> WithContext<TContext>()
                where TContext : class
            {
                return new UserContextBuilder<TContext>(_services);
            }
        }

        public struct UserContextBuilder<TContext>
            where TContext : class
        {
            private readonly IServiceCollection _services;

            public UserContextBuilder(IServiceCollection services) => _services = services;

            public DbContextBuilder<TContext, TContextKey> WithKey<TContextKey>(Expression<Func<TContext, TContextKey>> keySelector)
            {
                return new DbContextBuilder<TContext, TContextKey>(_services, new ContextKeySelector<TContext, TContextKey>(keySelector))
                    .AddAwaiterHandler<SessionCancelled>(c => c.WithAwaiter<SessionCancelledAwaiter>())
                    .AddAwaiterHandler<BeforeSessionCancelled>(c => c.WithAwaiter<BeforeSessionCancelledAwaiter>())
                    .AddAwaiterHandler<Unreachable>(c => c.WithAwaiter<UnreachableAwaiter>());
            }
        }

        public struct DbContextBuilder<TContext, TContextKey>
            where TContext : class
        {
            private readonly IServiceCollection _services;
            private readonly ContextKeySelector<TContext, TContextKey> _contextKeySelector;

            public DbContextBuilder(IServiceCollection services, ContextKeySelector<TContext, TContextKey> contextKeySelector)
            {
                _services = services;
                _contextKeySelector = contextKeySelector;
            }

            public UnitOfWorkBuilder<TDbContext, TContext, TContextKey> WithDbContext<TDbContext>(Func<IServiceProvider, TDbContext> createDbContext)
                where TDbContext : DbContext
            {
                return new UnitOfWorkBuilder<TDbContext, TContext, TContextKey>(_services, createDbContext, _contextKeySelector);
            }

            public UnitOfWorkBuilder<TDbContext, TContext, TContextKey> WithDbContext<TDbContext>(Func<TDbContext> createDbContext)
                where TDbContext : DbContext
            {
                return new UnitOfWorkBuilder<TDbContext, TContext, TContextKey>(_services, sp => createDbContext(), _contextKeySelector);
            }

            public DbContextBuilder<TContext, TContextKey> AddAwaiterHandler<TEvent>(Func<EventHandlerRegistrationBuilder<TEvent>, EventHandlerRegistrationBuilder<TEvent>> configure)
            {
                var regBuilder = configure(new EventHandlerRegistrationBuilder<TEvent>());
                if (regBuilder._awaiterHandlerType != null)
                    _services.AddSingleton(typeof(IAwaiterHandler<TContext, TContextKey>), regBuilder._awaiterHandlerType);
                else if (regBuilder._awaiterHandler != null)
                    _services.AddSingleton<IAwaiterHandler<TContext, TContextKey>>(regBuilder._awaiterHandler);
                else
                    _services.AddSingleton<IAwaiterHandler<TContext, TContextKey>>(new DefaultAwaiterHandler<TContext, TContextKey, TEvent>(
                        regBuilder._contextFilter,
                        regBuilder._awaiterIdTypes
                    ));

                return this;
            }

            public struct EventHandlerRegistrationBuilder<TEvent>
            {
                public Type? _awaiterHandlerType { get; private set; }
                public IAwaiterHandler<TContext, TContextKey, TEvent>? _awaiterHandler { get; private set; }
                public Func<TEvent, Expression<Func<SessionStateTable<TContext, TContextKey>, bool>>>? _contextFilter { get; private set; }
                public List<Type> _awaiterIdTypes { get; } = new List<Type>();

                public EventHandlerRegistrationBuilder()
                {
                }

                public EventHandlerRegistrationBuilder<TEvent> WithAwaiterHandler<TAwaiterHandler>()
                    where TAwaiterHandler: class, IAwaiterHandler<TContext, TContextKey, TEvent>
                {
                    _awaiterHandlerType = typeof(TAwaiterHandler);
                    return this;
                }

                public EventHandlerRegistrationBuilder<TEvent> WithAwaiterHandler<TAwaiterHandler>(TAwaiterHandler awaiterHandler)
                    where TAwaiterHandler : class, IAwaiterHandler<TContext, TContextKey, TEvent>
                {
                    _awaiterHandler = awaiterHandler;
                    return this;
                }

                public EventHandlerRegistrationBuilder<TEvent> WithContextFilter(Func<TEvent, Expression<Func<SessionStateTable<TContext, TContextKey>, bool>>> contextFilter)
                {
                    _contextFilter = contextFilter;
                    return this;
                }

                public EventHandlerRegistrationBuilder<TEvent> WithAwaiter<TAwaiterId>()
                    where TAwaiterId: IEventAwaiter
                {
                    var awaiterIdType = typeof(TAwaiterId);
                    if (awaiterIdType.GetConstructor(new[] { typeof(TEvent) }) == null && awaiterIdType.GetConstructor(new Type[0]) == null)
                        throw new ArgumentException($"AwaiterId {typeof(TAwaiterId).FullName} must have constructor with argument {typeof(TEvent)} or constructor without arguments");


                    _awaiterIdTypes.Add(typeof(TAwaiterId));
                    return this;
                }
            }
        }

        public struct UnitOfWorkBuilder<TDbContext, TContext, TContextKey>
            where TDbContext : DbContext
            where TContext : class
        {
            private readonly IServiceCollection _services;
            private readonly Func<IServiceProvider, TDbContext> _createDbContext;
            private readonly ContextKeySelector<TContext, TContextKey> _contextKeySelector;

            public UnitOfWorkBuilder(IServiceCollection services, Func<IServiceProvider, TDbContext> createDbContext, ContextKeySelector<TContext, TContextKey> contextKeySelector)
            {
                _services = services;
                _createDbContext = createDbContext;
                _contextKeySelector = contextKeySelector;
            }

            public IServiceCollection WithUnitOfWork<TUnitOfWork>()
                where TUnitOfWork : EFSessionStateUnitOfWork<TContext, TContextKey>, new()
            {
                return EFStateMachineBootstrapper.AddEFStateMachine<TUnitOfWork, TDbContext, TContext, TContextKey>(_services, _createDbContext, _contextKeySelector);
            }

            public IServiceCollection WithDefaultUnitOfWork() =>
                WithUnitOfWork<EFSessionStateUnitOfWork<TContext, TContextKey>>();
        }
    }
}
