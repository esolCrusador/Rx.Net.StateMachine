﻿using Rx.Net.StateMachine.EntityFramework.Awaiters;
using Rx.Net.StateMachine.EntityFramework.ContextDfinition;
using Rx.Net.StateMachine.EntityFramework.Tests.UnitOfWork;
using Rx.Net.StateMachine.EntityFramework.UnitOfWork;
using Rx.Net.StateMachine.Persistance;
using System.Text.Json;

public class EFSessionStateUnitOfWorkFactory<TContext, TContextKey, TUnitOfWork> : ISessionStateUnitOfWorkFactory
    where TContext : class
    where TUnitOfWork : EFSessionStateUnitOfWork<TContext, TContextKey>, new()
{
    private readonly SessionStateDbContextFactory _contextFactory;
    private readonly ContextKeySelector<TContext, TContextKey> _contextKeySelector;
    private readonly AwaitHandlerResolver<TContext, TContextKey> _awaitHandlerResolver;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public EFSessionStateUnitOfWorkFactory(SessionStateDbContextFactory contextFactory, ContextKeySelector<TContext, TContextKey> contextKeySelector, AwaitHandlerResolver<TContext, TContextKey> awaitHandlerResolver, JsonSerializerOptions jsonSerializerOptions)
    {
        _contextFactory = contextFactory;
        _contextKeySelector = contextKeySelector;
        _awaitHandlerResolver = awaitHandlerResolver;
        _jsonSerializerOptions = jsonSerializerOptions;
    }
    public ISessionStateUnitOfWork Create()
    {
        var uof = new TUnitOfWork
        {
            ContextFactory = _contextFactory,
            SessionStateDbContext = _contextFactory.CreateBase(),
            ContextKeySelector = _contextKeySelector,
            EventAwaiterResolver = _awaitHandlerResolver,
            JsonSerializerOptions = _jsonSerializerOptions
        };

        return uof;
    }
}