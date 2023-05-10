using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rx.Net.StateMachine.EntityFramework;
using Rx.Net.StateMachine.Persistance;
using Rx.Net.StateMachine.Tests.Controls;
using Rx.Net.StateMachine.Tests.DataAccess;
using Rx.Net.StateMachine.Tests.Fakes;
using Rx.Net.StateMachine.Tests.Persistence;
using Rx.Net.StateMachine.WorkflowFactories;
using System;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Tests.Testing
{
    public class StateMachineTestContextBuilder
    {
        private IServiceCollection _services;

        public StateMachineTestContextBuilder(IServiceCollection services) => 
            _services = services;

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
            where TWorkflowFactory: class, IWorkflow
        {
            _services.AddWorkflowFactory<TWorkflowFactory>();
            return this;
        }

        public StateMachineTestContext Build() =>
            new StateMachineTestContext(_services.BuildServiceProvider());

        public static IServiceCollection RegisterDefaultServices(Func<TestSessionStateDbContext> createContext)
        {
            var services = new ServiceCollection();
            services.AddSingleton(createContext);
            services.AddStateMachine<UserContext>();
            services.AddSingleton<ChatFake>();
            services.AddSingleton<MessageQueue>();
            services.AddSingleton<FakeScheduler>();
            services.AddControls();

            services.AddEFStateMachine()
                .WithContext<UserContext>()
                .WithKey(uc => uc.ContextId)
                .WithDbContext(createContext)
                .WithUnitOfWork<TestEFSessionStateUnitOfWork>();
            services.AddSingleton<UserContextRepository>();

            return services;
        }
        public static StateMachineTestContextBuilder Fast()
        {
            var databaseName = $"TestDatabase-{Guid.NewGuid()}";

            var services = RegisterDefaultServices(() => new TestSessionStateDbContext(new DbContextOptionsBuilder().UseInMemoryDatabase(databaseName).Options));

            return new StateMachineTestContextBuilder(services);
        }

        public static StateMachineTestContextBuilder Slow()
        {
            var databaseName = $"TestDatabase-{Guid.NewGuid()}";

            var services = RegisterDefaultServices(() => new TestSessionStateDbContext(new DbContextOptionsBuilder()
                    .UseSqlServer("Data Source =.; Integrated Security = True; TrustServerCertificate=True; Initial Catalog=TestDatabase;".Replace("TestDatabase", databaseName))
                    .Options));

            return new StateMachineTestContextBuilder(services);
        }
    }
}
