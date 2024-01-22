using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rx.Net.StateMachine.EntityFramework;
using Rx.Net.StateMachine.Events;
using Rx.Net.StateMachine.Exceptions;
using Rx.Net.StateMachine.Persistance;
using Rx.Net.StateMachine.Tests.Awaiters;
using Rx.Net.StateMachine.Tests.Concurrency;
using Rx.Net.StateMachine.Tests.Controls;
using Rx.Net.StateMachine.Tests.DataAccess;
using Rx.Net.StateMachine.Tests.Events;
using Rx.Net.StateMachine.Tests.Fakes;
using Rx.Net.StateMachine.Tests.Persistence;
using Rx.Net.StateMachine.WorkflowFactories;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using static Rx.Net.StateMachine.EntityFramework.EFStateMachineBootstrapper;

namespace Rx.Net.StateMachine.Tests.Testing
{
    public class StateMachineTestContextBuilder
    {
        private IServiceCollection _services;
        private readonly DbContextBuilder<UserContext, int> _dbContextBuilder;

        public StateMachineTestContextBuilder(IServiceCollection services, DbContextBuilder<UserContext, int> dbContextBuilder)
        {
            _services = services;
            _dbContextBuilder = dbContextBuilder;
        }

        public StateMachineTestContextBuilder Configure(Action<IServiceCollection> configure)
        {
            configure(_services);
            return this;
        }

        public StateMachineTestContextBuilder AddMessageHandler(Func<BotFrameworkMessage, Task> handleMessage)
        {
            _services.AddSingleton(sp => sp.GetRequiredService<ChatFake>().AddMessageHandler(handleMessage));
            return this;
        }

        public StateMachineTestContextBuilder AddClickHandler(Func<BotFrameworkButtonClick, Task> handleClick)
        {
            _services.AddSingleton(sp => sp.GetRequiredService<ChatFake>().AddClickHandler(handleClick));
            return this;
        }

        public StateMachineTestContextBuilder AddEventHandler<TEvent>(Func<TEvent, Task> handleEvent)
        {
            _services.AddSingleton(sp =>
            {
                var handler = sp.GetRequiredService<MessageQueue>().AddEventHandler(handleEvent);
                return new HandlerRegistration(handler);
            });
            return this;
        }

        public StateMachineTestContextBuilder AddWorkflow<TWorkflowFactory>()
            where TWorkflowFactory : class, IWorkflow
        {
            _services.AddWorkflow<TWorkflowFactory>();
            return this;
        }

        public StateMachineTestContextBuilder WithWorkflowFatal<TException>()
            where TException : Exception
        {
            _services.WithWorkflowFatal<TException>();
            return this;
        }

        public StateMachineTestContextBuilder WithWorkflowFatal<TException>(Func<TException, bool> filter)
            where TException : Exception
        {
            _services.WithWorkflowFatal<TException>(filter);
            return this;
        }

        public DbContextBuilder<UserContext, int> ForContextBuilder()
        {
            return _dbContextBuilder;
        }

        public StateMachineTestContext Build() =>
            new StateMachineTestContext(_services.BuildServiceProvider());

        public static (IServiceCollection Services, DbContextBuilder<UserContext, int> DbContextBuilder) RegisterDefaultServices(Func<GlobalContextState, TestSessionStateDbContext> createContext)
        {
            var services = new ServiceCollection();
            services.AddSingleton(createContext);
            services.AddStateMachine<UserContext>();
            services.AddSingleton<ChatFake>();
            services.AddSingleton<MessageQueue>();
            services.AddSingleton<FakeScheduler>();
            services.AddSingleton<GlobalContextState>();
            services.AddLogging();
            services.AddControls();
            services.AddSingleton(new JsonSerializerOptions());

            var dbContextBuilder = services.AddEFStateMachine()
                .WithContext<UserContext>()
                .WithKey(uc => uc.ContextId)
                .AddAwaiterHandler<BotFrameworkMessage>(c => c.WithAwaiter<BotFrameworkMessageAwaiter>())
                .AddAwaiterHandler<BotFrameworkButtonClick>(c => c.WithAwaiter<BotFrameworkButtonClickAwaiter>())
                .AddAwaiterHandler<TaskCreatedEvent>(c => c.WithAwaiter<TaskCreatedEventAwaiter>())
                .AddAwaiterHandler<TaskStateChanged>(c => c.WithAwaiter<TaskStateChangedAwaiter>())
                .AddAwaiterHandler<TimeoutEvent>(c => c.WithAwaiter<TimeoutEventAwaiter>())
                .AddAwaiterHandler<TaskCommentAdded>(c => c.WithContextFilter(ev =>
                {
                    var sessionIdString = ev.Context?.GetValueOrDefault("SessionId");
                    if (sessionIdString != null)
                    {
                        var sessionId = Guid.Parse(sessionIdString);
                        return ss => ss.SessionStateId != sessionId;
                    }
                    return ss => true;
                }).WithAwaiter<TaskCommentAddedAwaiter>())
                .AddAwaiterHandler<DefaultSessionRemoved>(c => c.WithContextFilter(ev =>
                {
                    var contextId = int.Parse(ev.UserContextId);
                    return ss => ss.IsDefault && ss.ContextId == contextId && ss.SessionStateId != ev.SessionId;
                }).WithAwaiter<DefaultSessionRemovedAwaiter>());


            dbContextBuilder.WithDbContext(sp => createContext(sp.GetRequiredService<GlobalContextState>()))
                .WithUnitOfWork<TestEFSessionStateUnitOfWork>();
            services.AddSingleton<UserContextRepository>();

            return (services, dbContextBuilder);
        }
        public static StateMachineTestContextBuilder Fast()
        {
            var databaseName = $"TestDatabase-{Guid.NewGuid()}";

            var (services, contextBuilder) = RegisterDefaultServices(gs => new TestSessionStateDbContext(gs, new DbContextOptionsBuilder()
                .EnableSensitiveDataLogging()
                .UseInMemoryDatabase(databaseName).Options)
            );
            services.AddSingleton(sp => new AsyncWait(TimeSpan.FromMilliseconds(100)));

            return new StateMachineTestContextBuilder(services, contextBuilder);
        }

        public static StateMachineTestContextBuilder Slow()
        {
            var databaseName = $"TestDatabase-{Guid.NewGuid()}";

            var (services, contextBuilder) = RegisterDefaultServices(gs => new TestSessionStateDbContext(gs, new DbContextOptionsBuilder()
                    .EnableSensitiveDataLogging()
                    .UseSqlServer("Data Source =.; Integrated Security = True; TrustServerCertificate=True; Initial Catalog=TestDatabase;".Replace("TestDatabase", databaseName))
                    .Options));
            services.AddSingleton(sp => new AsyncWait(TimeSpan.FromSeconds(20)));

            return new StateMachineTestContextBuilder(services, contextBuilder);
        }
    }
}
