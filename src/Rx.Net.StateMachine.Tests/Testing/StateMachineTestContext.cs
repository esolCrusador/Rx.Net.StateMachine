using Microsoft.Extensions.DependencyInjection;
using Rx.Net.StateMachine.EntityFramework.ContextDfinition;
using Rx.Net.StateMachine.Persistance;
using Rx.Net.StateMachine.Tests.DataAccess;
using Rx.Net.StateMachine.Tests.Fakes;
using Rx.Net.StateMachine.Tests.Persistence;
using Rx.Net.StateMachine.WorkflowFactories;
using System;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Tests.Testing
{
    public class StateMachineTestContext : IAsyncDisposable
    {
        private readonly ServiceProvider _services;

        public WorkflowManager<UserContext> WorkflowManager => _services.GetRequiredService<WorkflowManager<UserContext>>();
        public ChatFake Chat => _services.GetRequiredService<ChatFake>();
        public SessionStateDbContextFactory<TestSessionStateDbContext, UserContext, int> ContextFactory =>
            _services.GetRequiredService<SessionStateDbContextFactory<TestSessionStateDbContext, UserContext, int>>();
        public UserContextRepository UserContextRepository => _services.GetRequiredService<UserContextRepository>();
        public StateMachine StateMachine => _services.GetRequiredService<StateMachine>();
        public IWorkflowResolver WorkflowResolver => _services.GetRequiredService<IWorkflowResolver>();
        public FakeScheduler Scheduler => _services.GetRequiredService<FakeScheduler>();
        public AsyncWait AsyncWait => _services.GetRequiredService<AsyncWait>();

        public StateMachineTestContext(ServiceProvider serviceProvider) =>
            _services = serviceProvider;

        public async Task InititalizeAsync()
        {
            _services.GetServices<HandlerRegistration>();

            await using var context = ContextFactory.Create();
            await context.Database.EnsureCreatedAsync();
        }

        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);

            await using var context = ContextFactory.Create();
            await context.Database.EnsureDeletedAsync();

            await _services.DisposeAsync();
        }

        public TService GetService<TService>() where TService : class 
            => _services.GetRequiredService<TService>();
    }
}
