using FluentAssertions;
using Rx.Net.StateMachine.ObservableExtensions;
using Rx.Net.StateMachine.States;
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
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Rx.Net.StateMachine.Tests
{
    public class BotStatefulDialogTests : IDisposable
    {
        private IDisposable _buttonClickSubscription;
        private readonly StateMachine _stateMachine;
        private readonly Guid _userId = Guid.NewGuid();
        private readonly WorkflowResolver _workflowResolver;
        private readonly WorkflowManager<TestSessionStateEntity, UserContext> _workflowManager;
        private readonly BotFake _botFake;
        private readonly ItemsManager _itemsManager;

        public BotStatefulDialogTests()
        {
            _botFake = new BotFake();
            var dataStore = new SessionStateDataStore<TestSessionStateEntity>();
            _itemsManager = new ItemsManager(
                new Item { Id = Guid.NewGuid(), Name = "Task 1", Status = ItemStatus.ToDo },
                new Item { Id = Guid.NewGuid(), Name = "Task 2", Status = ItemStatus.ToDo },
                new Item { Id = Guid.NewGuid(), Name = "Task 3", Status = ItemStatus.InProgress }
            );

            _stateMachine = new StateMachine(new JsonSerializerOptions());
            var workflowManagerAccessor = new WorkflowManagerAccessor<TestSessionStateEntity, UserContext>();
            _workflowResolver = new WorkflowResolver(
                new TaskActionsWorkflowFactory(_botFake, _itemsManager, _stateMachine, workflowManagerAccessor),
                new EditItemWorkflowFactory(_botFake, _itemsManager)
            );
            _workflowManager = new WorkflowManager<TestSessionStateEntity, UserContext>(
                new TestSessionStateContext(),
                new JsonSerializerOptions(),
                () => new SessionStateUnitOfWork(dataStore),
                _workflowResolver
            );
            workflowManagerAccessor.Initialize(_workflowManager);
            _buttonClickSubscription = _botFake.ButtonClick.SelectAsync(click => HandleButtonClick(click)).Merge().Subscribe();
        }

        public void Dispose()
        {
            _buttonClickSubscription.Dispose();
        }

        [Fact]
        public async Task Should_React_On_Play_Button()
        {
            await ShowItems();
            var messages = _botFake.ReadNewBotMessages(_userId);
            var secondMessage = messages.Skip(1).First();

            await _botFake.ClickButton(secondMessage, secondMessage.Buttons.First().Value);
            var updatedMessage = _botFake.ReadMessage(_userId, secondMessage.MessageId);
            updatedMessage.Buttons.First().Key.Should().Be("Pause");
        }

        [Fact]
        public async Task Should_React_On_Pause_Button()
        {
            await ShowItems();
            var messages = _botFake.ReadNewBotMessages(_userId);
            var secondMessage = messages.Skip(1).First();

            await _botFake.ClickButton(secondMessage, secondMessage.Buttons.First().Value);
            secondMessage = _botFake.ReadMessage(_userId, secondMessage.MessageId);
            await _botFake.ClickButton(secondMessage, secondMessage.Buttons.First().Value);
            secondMessage = _botFake.ReadMessage(_userId, secondMessage.MessageId);
            secondMessage.Buttons.First().Key.Should().Be("Play");
        }

        [Fact]
        public async Task Should_React_On_Delete_Button()
        {
            await ShowItems();
            var messages = _botFake.ReadNewBotMessages(_userId);
            var secondMessage = messages.Skip(1).First();

            await _botFake.ClickButton(secondMessage, secondMessage.Buttons.Last().Value);
            secondMessage = _botFake.ReadMessage(_userId, secondMessage.MessageId);
            secondMessage.Should().BeNull();
            _itemsManager.GetItems().Count.Should().Be(2);
        }

        [Fact]
        public async Task Should_Set_Name()
        {
            await ShowItems();

            var messages = _botFake.ReadNewBotMessages(_userId);
            var secondMessage = messages.Skip(1).First();

            await _botFake.ClickButton(secondMessage, secondMessage.Buttons.Skip(1).First().Value);
            var confirmation = _botFake.ReadNewBotMessages(_userId).Single();

            await _botFake.ClickButton(secondMessage, confirmation.Buttons.First(b => b.Key == "Yes").Value);
        }

        private async Task ShowItems()
        {
            foreach (var item in _itemsManager.GetItems())
                await ShowItem(item);
        }

        private async Task ShowItem(Item item)
        {
            var context = new DialogContext { UserId = _userId };

            await _stateMachine.StartHandleWorkflow(item, context, await _workflowResolver.GetWorkflowFactory<Item, Unit>(TaskActionsWorkflowFactory.Id));
        }

        private async Task HandleMessage(BotFrameworkMessage message)
        {
            await _workflowManager.HandleEvent(message, new DialogContext { UserId = message.UserId, MessageId = message.MessageId });
        }

        private async Task HandleButtonClick(BotFrameworkButtonClick buttonClick)
        {
            if (buttonClick.SelectedValue.StartsWith("s:"))
            {
                var context = new DialogContext { UserId = buttonClick.UserId, MessageId = buttonClick.MessageId };
                var stateString = buttonClick.SelectedValue.Substring(2, buttonClick.SelectedValue.LastIndexOf('-') - 2);
                var state = _stateMachine.ParseSessionState(context, stateString);
                _stateMachine.ForceAddEvent(state, buttonClick);

                await _stateMachine.HandleWorkflow(state, await _workflowResolver.GetWorkflowFactory(state.WorkflowId));
            }
            else
            {
                await _workflowManager.HandleEvent(buttonClick, new DialogContext { UserId = buttonClick.UserId, MessageId = buttonClick.MessageId });
            }
        }

        // https://www.figma.com/file/JXTrJQklRBTbbGbvhI0taD/Task-Actions-Dialog?node-id=3%3A76
        class TaskActionsWorkflowFactory : WorkflowFactory<Item, Unit>
        {
            private readonly BotFake _botFake;
            private readonly ItemsManager _itemsManager;
            private readonly StateMachine _stateMachine;
            private readonly WorkflowManagerAccessor<TestSessionStateEntity, UserContext> _workflowManagerAccessor;

            public TaskActionsWorkflowFactory(BotFake botFake, ItemsManager itemsManager, StateMachine stateMachine, WorkflowManagerAccessor<TestSessionStateEntity, UserContext> workflowManagerAccessor)
            {
                _botFake = botFake;
                _itemsManager = itemsManager;
                _stateMachine = stateMachine;
                _workflowManagerAccessor = workflowManagerAccessor;
            }

            public const string Id = "task-actions";
            public override string WorkflowId => Id;

            public override IObservable<Unit> GetResult(IObservable<Item> input, StateMachineScope scope)
            {
                var context = scope.GetContext<DialogContext>();

                return input
                    .Persist(scope, "Item")
                    .Select(item =>
                    {
                        return Observable.FromAsync(() =>
                        {
                            var currentState = scope.GetStateString();

                            return _botFake.AddOrUpdateBotMessage(context.UserId, context.MessageId, $"{item.Name}",
                                    new KeyValuePair<string, string>(item.Status == ItemStatus.ToDo ? "Play" : "Pause", $"s:{currentState}-{(item.Status == ItemStatus.ToDo ? "pl" : "pa")}"),
                                    new KeyValuePair<string, string>("Edit", $"s:{currentState}-e"),
                                    new KeyValuePair<string, string>("Delete", $"s:{currentState}-d")
                                );
                        }).PersistBeforePrevious(scope, "InitialDialog")
                        .Select(_ => context.MessageId)
                        .StopAndWait().For<BotFrameworkButtonClick>(scope, "InitialButtonClock")
                        .SelectAsync(async buttonClick =>
                        {
                            string selectedValue = buttonClick.SelectedValue.Substring(buttonClick.SelectedValue.LastIndexOf('-') + 1);
                            switch (selectedValue)
                            {
                                case "pl":
                                    {
                                        var updatedItem = await _itemsManager.UpdateItem(item.Id, i => i.Status = ItemStatus.InProgress);
                                        await UpdateItemMessage(updatedItem, new DialogContext { UserId = context.UserId, MessageId = context.MessageId }); ;
                                        break;
                                    }
                                case "pa":
                                    {
                                        var updatedItem = await _itemsManager.UpdateItem(item.Id, i => i.Status = ItemStatus.ToDo);
                                        await UpdateItemMessage(updatedItem, new DialogContext { UserId = context.UserId, MessageId = context.MessageId }); ;
                                        break;
                                    }
                                case "e":
                                    await _workflowManagerAccessor.WorkflowManager.StartHandle(item, EditItemWorkflowFactory.Id, context);
                                    break;
                                case "d":
                                    {
                                        await _itemsManager.DeleteItem(item.Id);
                                        await _botFake.DeleteBotMessage(context.UserId, context.MessageId.Value);
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

            private async Task UpdateItemMessage(Item item, DialogContext dialogContext)
            {
                await _stateMachine.StartHandleWorkflow(item, dialogContext, this);
            }
        }

        class EditItemWorkflowFactory : WorkflowFactory<Item, Unit>
        {
            private readonly BotFake _botFake;
            private readonly ItemsManager _itemsManager;

            public EditItemWorkflowFactory(BotFake botFake, ItemsManager itemsManager)
            {
                _botFake = botFake;
                _itemsManager = itemsManager;
            }

            public const string Id = "edit-item";
            public override string WorkflowId => Id;

            public override IObservable<Unit> GetResult(IObservable<Item> input, StateMachineScope scope)
            {
                var context = scope.GetContext<DialogContext>();

                return input.Persist(scope, "Item").Select(item =>
                {
                    return Observable.FromAsync(() => _botFake.SendButtonsBotMessage(context.UserId, "Do you want to change name?", context.MessageId,
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
                                await _botFake.DeleteBotMessage(context.UserId, messageId);
                        })
                    ).Concat()
                    .Select(changeName =>
                    {
                        if (!changeName)
                            return StateMachineObservableExtensions.Of(Unit.Default);

                        return UpdateItemName(item, scope.BeginRecursiveScope("Name"));
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
                        var context = scope.GetContext<DialogContext>();

                        return Observable.FromAsync(() =>
                            _botFake.SendBotMessage(context.UserId, "Please enter name"))
                                .PersistMessageId(scope)
                                .Select(messageId =>
                                    scope.StopAndWait<BotFrameworkMessage>("NameInput")
                                    .Select(nameInput =>
                                    {
                                        if (string.IsNullOrEmpty(nameInput.Text))
                                            return Observable.FromAsync(() => _botFake.SendBotMessage(context.UserId, "Name is not valid", nameInput.MessageId))
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

        class DialogContext : UserContext
        {
            public int? MessageId { get; set; }
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
    }
}
