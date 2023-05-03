using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rx.Net.StateMachine.EntityFramework;
using Rx.Net.StateMachine.EntityFramework.ContextDfinition;
using Rx.Net.StateMachine.ObservableExtensions;
using Rx.Net.StateMachine.Persistance;
using Rx.Net.StateMachine.Tests.DataAccess;
using Rx.Net.StateMachine.Tests.Extensions;
using Rx.Net.StateMachine.Tests.Fakes;
using Rx.Net.StateMachine.Tests.Persistence;
using Rx.Net.StateMachine.WorkflowFactories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Rx.Net.StateMachine.Tests
{
    public abstract class BotStatefulDialogTests : IAsyncLifetime
    {
        private readonly long _botId = new Random().NextInt64(long.MaxValue);
        private long _chatId;
        private readonly ServiceProvider _services;

        private StateMachine _stateMachine => _services.GetRequiredService<StateMachine>();
        private IWorkflowResolver _workflowResolver => _services.GetRequiredService<IWorkflowResolver>();
        private WorkflowManager<UserContext> _workflowManager => _services.GetRequiredService<WorkflowManager<UserContext>>();
        private ChatFake ChatFake => _services.GetRequiredService<ChatFake>();
        private ItemsManager _itemsManager => _services.GetRequiredService<ItemsManager>();
        private UserContextRepository UserContextRepository => _services.GetRequiredService<UserContextRepository>();
        private SessionStateDbContextFactory<TestSessionStateDbContext, UserContext, int> _contextFactory =>
            _services.GetRequiredService<SessionStateDbContextFactory<TestSessionStateDbContext, UserContext, int>>();

        private BotStatefulDialogTests(Func<TestSessionStateDbContext> createContext)
        {
            var services = new ServiceCollection();
            services.AddSingleton(createContext);
            services.AddSingleton<ChatFake>();
            services.AddSingleton(new ItemsManager(
                new Item { Id = Guid.NewGuid(), Name = "Task 1", Status = ItemStatus.ToDo },
                new Item { Id = Guid.NewGuid(), Name = "Task 2", Status = ItemStatus.ToDo },
                new Item { Id = Guid.NewGuid(), Name = "Task 3", Status = ItemStatus.InProgress }
            ));
            services.AddStateMachine<UserContext>()
                .AddWorkflowFactory<TaskActionsWorkflowFactory>()
                .AddWorkflowFactory<EditItemWorkflowFactory>();
            services.AddEFStateMachine()
                .WithContext<UserContext>()
                .WithKey(uc => uc.ContextId)
                .WithDbContext(createContext)
                .WithUnitOfWork<TestEFSessionStateUnitOfWork>();
            services.AddSingleton<UserContextRepository>();
            services.AddSingleton(ss => ss.GetRequiredService<ChatFake>().AddClickHandler(click => HandleButtonClick(click)));
            _services = services.BuildServiceProvider();

            _services.GetServices<HandlerRegistration>();
        }

        [Trait("Category", "Fast")]
        public class Fast : BotStatefulDialogTests
        {
            public Fast() : this($"TestDatabase-{Guid.NewGuid()}")
            {
            }

            private Fast(string databaseName) :
                base(() => new TestSessionStateDbContext(new DbContextOptionsBuilder().UseInMemoryDatabase(databaseName).Options))
            {
            }
        }

        [Trait("Category", "Slow")]
        public class Slow : BotStatefulDialogTests
        {
            public Slow() : this($"TestDatabase-{Guid.NewGuid()}")
            {

            }

            private Slow(string databaseName) :
                base(() => new TestSessionStateDbContext(new DbContextOptionsBuilder()
                    .UseSqlServer("Data Source =.; Integrated Security = True; TrustServerCertificate=True; Initial Catalog=TestDatabase;".Replace("TestDatabase", databaseName))
                    .Options))
            {

            }
        }

        public async Task InitializeAsync()
        {
            await using var context = _contextFactory.Create();
            await context.Database.EnsureCreatedAsync();

            _chatId = await ChatFake.RegisterUser(new UserInfo
            {
                FirstName = "Boris",
                LastName = "Sotsky",
                Username = "esolCrusador"
            });
        }

        public async Task DisposeAsync()
        {
            await using var context = _contextFactory.Create();
            await context.Database.EnsureDeletedAsync();

            await _services.DisposeAsync();
        }

        [Fact]
        public async Task Should_React_On_Play_Button()
        {
            await ShowItems();
            var messages = ChatFake.ReadNewBotMessages(_botId, _chatId);
            var secondMessage = messages.Skip(1).First();

            await ChatFake.ClickButton(secondMessage, secondMessage.Buttons.First().Value);
            var updatedMessage = ChatFake.ReadMessage(_botId, _chatId, secondMessage.MessageId);
            updatedMessage.Buttons.First().Key.Should().Be("Pause");
        }

        [Fact]
        public async Task Should_React_On_Pause_Button()
        {
            await ShowItems();
            var messages = ChatFake.ReadNewBotMessages(_botId, _chatId);
            var secondMessage = messages.Skip(1).First();

            await ChatFake.ClickButton(secondMessage, secondMessage.Buttons.First().Value);
            secondMessage = ChatFake.ReadMessage(_botId, _chatId, secondMessage.MessageId);
            await ChatFake.ClickButton(secondMessage, secondMessage.Buttons.First().Value);
            secondMessage = ChatFake.ReadMessage(_botId, _chatId, secondMessage.MessageId);
            secondMessage.Buttons.First().Key.Should().Be("Play");
        }

        [Fact]
        public async Task Should_React_On_Delete_Button()
        {
            await ShowItems();
            var messages = ChatFake.ReadNewBotMessages(_botId, _chatId);
            var secondMessage = messages.Skip(1).First();

            await ChatFake.ClickButton(secondMessage, secondMessage.Buttons.Last().Value);
            secondMessage = ChatFake.ReadMessage(_botId, _chatId, secondMessage.MessageId);
            secondMessage.Should().BeNull();
            _itemsManager.GetItems().Count.Should().Be(2);
        }

        [Fact]
        public async Task Should_Set_Name()
        {
            await ShowItems();

            var messages = ChatFake.ReadNewBotMessages(_botId, _chatId);
            var secondMessage = messages.Skip(1).First();

            await ChatFake.ClickButton(secondMessage, secondMessage.Buttons.Skip(1).First().Value);
            var confirmation = ChatFake.ReadNewBotMessages(_botId, _chatId).Single();

            await ChatFake.ClickButton(secondMessage, confirmation.Buttons.First(b => b.Key == "Yes").Value);
        }

        private async Task ShowItems()
        {
            foreach (var item in _itemsManager.GetItems())
                await ShowItem(item);
        }

        private async Task ShowItem(Item item)
        {
            var context = await UserContextRepository.GetUserOrCreateContext(_botId, _chatId, "Boris Sotsky", "esolCrusador");

            await _stateMachine.StartHandleWorkflow(new ItemWithMessage(item, null), context,
                await _workflowResolver.GetWorkflowFactory<ItemWithMessage, Unit>(TaskActionsWorkflowFactory.Id)
            );
        }

        private async Task HandleButtonClick(BotFrameworkButtonClick buttonClick)
        {
            if (buttonClick.SelectedValue.StartsWith("s:"))
            {
                var context = await UserContextRepository.GetUserContext(buttonClick.BotId, buttonClick.ChatId);
                var stateString = buttonClick.SelectedValue.Substring(2, buttonClick.SelectedValue.LastIndexOf('-') - 2);
                var state = _stateMachine.ParseSessionState(context, stateString);
                _stateMachine.ForceAddEvent(state, buttonClick);

                await _stateMachine.HandleWorkflow(state, await _workflowResolver.GetWorkflowFactory(state.WorkflowId));
            }
            else
            {
                await _workflowManager.HandleEvent(buttonClick);
            }
        }

        // https://www.figma.com/file/JXTrJQklRBTbbGbvhI0taD/Task-Actions-Dialog?node-id=3%3A76
        class TaskActionsWorkflowFactory : WorkflowFactory<ItemWithMessage, Unit>
        {
            private readonly ChatFake _chatFake;
            private readonly ItemsManager _itemsManager;
            private readonly StateMachine _stateMachine;
            private readonly WorkflowManagerAccessor<UserContext> _workflowManagerAccessor;

            public TaskActionsWorkflowFactory(ChatFake chatFake, ItemsManager itemsManager, StateMachine stateMachine, WorkflowManagerAccessor<UserContext> workflowManagerAccessor)
            {
                _chatFake = chatFake;
                _itemsManager = itemsManager;
                _stateMachine = stateMachine;
                _workflowManagerAccessor = workflowManagerAccessor;
            }

            public const string Id = "task-actions";
            public override string WorkflowId => Id;

            public override IObservable<Unit> GetResult(IObservable<ItemWithMessage> input, StateMachineScope scope)
            {
                var context = scope.GetContext<UserContext>();

                return input
                    .Persist(scope, "Item")
                    .Select(itemWithMessage =>
                    {
                        var item = itemWithMessage.Item;
                        return Observable.FromAsync(() =>
                        {
                            var currentState = scope.GetStateString();

                            return _chatFake.AddOrUpdateBotMessage(context.BotId, context.ChatId, itemWithMessage.MessageId, $"{item.Name}",
                                    new KeyValuePair<string, string>(item.Status == ItemStatus.ToDo ? "Play" : "Pause", $"s:{currentState}-{(item.Status == ItemStatus.ToDo ? "pl" : "pa")}"),
                                    new KeyValuePair<string, string>("Edit", $"s:{currentState}-e"),
                                    new KeyValuePair<string, string>("Delete", $"s:{currentState}-d")
                                );
                        }).PersistBeforePrevious(scope, "InitialDialog")
                        .StopAndWait().For<BotFrameworkButtonClick>(scope, "InitialButtonClock")
                        .SelectAsync(async buttonClick =>
                        {
                            string selectedValue = buttonClick.SelectedValue.Substring(buttonClick.SelectedValue.LastIndexOf('-') + 1);
                            switch (selectedValue)
                            {
                                case "pl":
                                    {
                                        var updatedItem = await _itemsManager.UpdateItem(item.Id, i => i.Status = ItemStatus.InProgress);
                                        await UpdateItemMessage(new ItemWithMessage(updatedItem, buttonClick.MessageId), context); ;
                                        break;
                                    }
                                case "pa":
                                    {
                                        var updatedItem = await _itemsManager.UpdateItem(item.Id, i => i.Status = ItemStatus.ToDo);
                                        await UpdateItemMessage(new ItemWithMessage(updatedItem, buttonClick.MessageId), context); ;
                                        break;
                                    }
                                case "e":
                                    await _workflowManagerAccessor.WorkflowManager.StartHandle(new ItemWithMessage(item, buttonClick.MessageId), EditItemWorkflowFactory.Id, context);
                                    break;
                                case "d":
                                    {
                                        await _itemsManager.DeleteItem(item.Id);
                                        await _chatFake.DeleteBotMessage(context.BotId, context.ChatId, buttonClick.MessageId);
                                        break;
                                    }
                                default:
                                    throw new NotSupportedException(buttonClick.SelectedValue);
                            }
                        })
                        .Concat();
                    })
                    .Concat()
                    .Select(_ => Unit.Default);
            }

            private async Task UpdateItemMessage(ItemWithMessage item, UserContext dialogContext)
            {
                await _stateMachine.StartHandleWorkflow(item, dialogContext, this);
            }
        }

        class EditItemWorkflowFactory : WorkflowFactory<ItemWithMessage, Unit>
        {
            private readonly ChatFake _botFake;
            private readonly ItemsManager _itemsManager;

            public EditItemWorkflowFactory(ChatFake botFake, ItemsManager itemsManager)
            {
                _botFake = botFake;
                _itemsManager = itemsManager;
            }

            public const string Id = "edit-item";
            public override string WorkflowId => Id;

            public override IObservable<Unit> GetResult(IObservable<ItemWithMessage> input, StateMachineScope scope)
            {
                var context = scope.GetContext<UserContext>();

                return input.Persist(scope, "Item").Select(item =>
                {
                    return Observable.FromAsync(() => _botFake.SendButtonsBotMessage(context.BotId, context.ChatId, "Do you want to change name?", item.MessageId,
                        new KeyValuePair<string, string>("Yes", "yes"),
                        new KeyValuePair<string, string>("No", "no")
                    ))
                    .Persist(scope, "ChangeNameConfirmation")
                    .PersistMessageId(scope)
                    .Select(messageId =>
                        scope.StopAndWait<BotFrameworkButtonClick>("ChangeNameConfirmationWait")
                        .Select(click => click.SelectedValue == "yes")
                        .FinallyAsync(async (isExecuted, _, ex) =>
                        {
                            if (isExecuted)
                                await _botFake.DeleteBotMessage(context.BotId, context.ChatId, messageId);
                        })
                    ).Concat()
                    .Select(changeName =>
                    {
                        if (!changeName)
                            return StateMachineObservableExtensions.Of(Unit.Default);

                        return UpdateItemName(item.Item, scope.BeginRecursiveScope("Name"));
                    })
                    .Concat()
                    .SelectAsync(_ => scope.DeleteMessageIds())
                    .Concat();
                }).Concat();
            }

            private IObservable<Unit> UpdateItemName(Item item, Task<StateMachineScope> scopeTask)
            {
                return scopeTask.ToObservable()
                    .Select(scope =>
                    {
                        var context = scope.GetContext<UserContext>();

                        return Observable.FromAsync(() =>
                            _botFake.SendBotMessage(context.BotId, context.ChatId, "Please enter name"))
                                .PersistMessageId(scope)
                                .Select(messageId =>
                                    scope.StopAndWait<BotFrameworkMessage>("NameInput")
                                    .Select(nameInput =>
                                    {
                                        if (string.IsNullOrEmpty(nameInput.Text))
                                            return Observable.FromAsync(() => _botFake.SendBotMessage(context.BotId, context.ChatId, "Name is not valid", messageId))
                                                .PersistMessageId(scope)
                                                .Select(_ =>
                                                        UpdateItemName(item, scope.IncreaseRecursionDepth())
                                                ).Concat();

                                        return Observable.FromAsync(() => _itemsManager.UpdateItem(item.Id, i => i.Name = nameInput.Text))
                                            .Select(_ => Unit.Default);
                                    })
                                    .Concat()
                                    .DeleteMssages(scope, _botFake)
                                ).Concat();
                    }).Concat();
            }
        }

        class ItemsManager
        {
            private Dictionary<Guid, Item> _items;

            public ItemsManager(params Item[] items) =>
                _items = items.ToDictionary(i => i.Id);

            public IReadOnlyCollection<Item> GetItems() => _items.Values;
            public Item GetItem(Guid itemId) => _items[itemId];
            public Task<Item> UpdateItem(Guid itemId, Action<Item> update)
            {
                var item = _items[itemId];
                update(item);
                return Task.FromResult(item);
            }
            public Task DeleteItem(Guid itemId)
            {
                _items.Remove(itemId);
                return Task.CompletedTask;
            }
        }

        enum ItemStatus
        {
            ToDo,
            InProgress,
            Done
        }

        class Item
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public ItemStatus Status { get; set; }
        }

        class ItemWithMessage
        {
            public Item Item { get; }
            public int? MessageId { get; }
            public ItemWithMessage(Item item, int? messageId)
            {
                Item = item;
                MessageId = messageId;
            }
        }
    }
}
