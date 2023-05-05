﻿using Microsoft.Extensions.DependencyInjection;
using Rx.Net.StateMachine.EntityFramework.ContextDfinition;
using Rx.Net.StateMachine.EntityFramework.Tests.UnitOfWork;
using Rx.Net.StateMachine.EntityFramework.UnitOfWork;
using Rx.Net.StateMachine.Persistance;
using System;
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
            where TDbContext: SessionStateDbContext<TContext, TContextKey>
            where TUnitOfWork: EFSessionStateUnitOfWork<TContext, TContextKey>, new()
            where TContext: class
        {
            services.AddSingleton(
                sp => new SessionStateDbContextFactory<TDbContext, TContext, TContextKey>(() => createDbContext(sp))
            );
            services.AddSingleton(contextKeySelector);
            services.AddSingleton<SessionStateDbContextFactory<TContext, TContextKey>>(
                sp => sp.GetRequiredService<SessionStateDbContextFactory<TDbContext, TContext, TContextKey>>()
            );
            services.AddSingleton<ISessionStateUnitOfWorkFactory, EFSessionStateUnitOfWorkFactory<TContext, TContextKey, TUnitOfWork>>();

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
                where TContext: class
            {
                return new UserContextBuilder<TContext>(_services);
            }
        }

        public struct UserContextBuilder<TContext>
            where TContext: class
        {
            private readonly IServiceCollection _services;

            public UserContextBuilder(IServiceCollection services) => _services = services;

            public DbContextBuilder<TContext, TContextKey> WithKey<TContextKey>(Expression<Func<TContext, TContextKey>> keySelector)
            {
                return new DbContextBuilder<TContext, TContextKey>(_services, new ContextKeySelector<TContext, TContextKey>(keySelector));
            }
        }

        public struct DbContextBuilder<TContext, TContextKey>
            where TContext: class
        {
            private readonly IServiceCollection _services;
            private readonly ContextKeySelector<TContext, TContextKey> _contextKeySelector;

            public DbContextBuilder(IServiceCollection services, ContextKeySelector<TContext, TContextKey> contextKeySelector)
            {
                _services = services;
                _contextKeySelector = contextKeySelector;
            }

            public UnitOfWorkBuilder<TDbContext, TContext, TContextKey> WithDbContext<TDbContext>(Func<IServiceProvider, TDbContext> createDbContext)
                where TDbContext: SessionStateDbContext<TContext, TContextKey>
            {
                return new UnitOfWorkBuilder<TDbContext, TContext, TContextKey>(_services, createDbContext, _contextKeySelector);
            }

            public UnitOfWorkBuilder<TDbContext, TContext, TContextKey> WithDbContext<TDbContext>(Func<TDbContext> createDbContext)
                where TDbContext : SessionStateDbContext<TContext, TContextKey>
            {
                return new UnitOfWorkBuilder<TDbContext, TContext, TContextKey>(_services, sp => createDbContext(), _contextKeySelector);
            }
        }

        public struct UnitOfWorkBuilder<TDbContext, TContext, TContextKey>
            where TDbContext: SessionStateDbContext<TContext, TContextKey>
            where TContext: class
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
                where TUnitOfWork: EFSessionStateUnitOfWork<TContext, TContextKey>, new()
            {
                return EFStateMachineBootstrapper.AddEFStateMachine<TUnitOfWork, TDbContext, TContext, TContextKey>(_services, _createDbContext, _contextKeySelector);
            }
        }
    }

    
}