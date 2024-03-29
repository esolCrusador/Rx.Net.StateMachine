﻿using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Rx.Net.StateMachine.Extensions;
using Rx.Net.StateMachine.Flow;
using Rx.Net.StateMachine.Persistance;
using Rx.Net.StateMachine.Tests.Awaiters;
using Rx.Net.StateMachine.Tests.Extensions;
using Rx.Net.StateMachine.Tests.Fakes;
using Rx.Net.StateMachine.Tests.Persistence;
using Rx.Net.StateMachine.Tests.Testing;
using Rx.Net.StateMachine.WorkflowFactories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Rx.Net.StateMachine.Tests
{
    public abstract class BotStatefulDialogTests : IAsyncLifetime
    {
        private readonly StateMachineTestContext _ctx;

        private readonly ItemsManager _itemsManager;
        private readonly long _botId = new Random().NextInt64(long.MaxValue);
        private long _chatId;

        private BotStatefulDialogTests(StateMachineTestContextBuilder builder)
        {
            _itemsManager = new ItemsManager(
                new Item { Id = Guid.NewGuid(), Name = "Task 1", Status = ItemStatus.ToDo },
                new Item { Id = Guid.NewGuid(), Name = "Task 2", Status = ItemStatus.ToDo },
                new Item { Id = Guid.NewGuid(), Name = "Task 3", Status = ItemStatus.InProgress }
            );
            builder.AddWorkflow<TaskActionsWorkflowFactory>()
                .AddWorkflow<EditItemWorkflowFactory>()
                .AddClickHandler(HandleButtonClick)
                .Configure(s => s.AddSingleton(_itemsManager));

            _ctx = builder.Build();
        }

        [Trait("Category", "Fast")]
        public class Fast : BotStatefulDialogTests
        {
            public Fast() : base(StateMachineTestContextBuilder.Fast())
            {
            }
        }

        [Trait("Category", "Slow")]
        public class Slow : BotStatefulDialogTests
        {
            public Slow() : base(StateMachineTestContextBuilder.Slow())
            {
            }
        }

        public async Task InitializeAsync()
        {
            await _ctx.InititalizeAsync();

            _chatId = await _ctx.Chat.RegisterUser(new UserInfo
            {
                FirstName = "Boris",
                LastName = "Sotsky",
                Username = "esolCrusador"
            });
        }

        public async Task DisposeAsync()
        {
            await _ctx.DisposeAsync();
        }

        [Fact]
        public async Task Should_React_On_Play_Button()
        {
            await ShowItems();
            var messages = _ctx.Chat.ReadNewBotMessages(_botId, _chatId);
            var secondMessage = messages.Skip(1).First();

            await _ctx.Chat.ClickButton(secondMessage, secondMessage.Buttons!.First().Value);
            var updatedMessage = _ctx.Chat.ReadMessage(_botId, _chatId, secondMessage.MessageId);
            updatedMessage!.Buttons!.First().Key.Should().Be("Pause");
        }

        [Fact]
        public async Task Should_React_On_Pause_Button()
        {
            await ShowItems();
            var messages = _ctx.Chat.ReadNewBotMessages(_botId, _chatId);
            var secondMessage = messages.Skip(1).First();

            await _ctx.Chat.ClickButton(secondMessage, secondMessage.Buttons!.First().Value);
            secondMessage = _ctx.Chat.ReadMessage(_botId, _chatId, secondMessage.MessageId);
            await _ctx.Chat.ClickButton(secondMessage!, secondMessage!.Buttons!.First().Value);
            secondMessage = _ctx.Chat.ReadMessage(_botId, _chatId, secondMessage.MessageId);
            secondMessage!.Buttons!.First().Key.Should().Be("Play");
        }

        [Fact]
        public async Task Should_React_On_Delete_Button()
        {
            await ShowItems();
            var messages = _ctx.Chat.ReadNewBotMessages(_botId, _chatId);
            var secondMessage = messages.Skip(1).First();

            await _ctx.Chat.ClickButton(secondMessage, secondMessage.Buttons!.Last().Value);
            secondMessage = _ctx.Chat.ReadMessage(_botId, _chatId, secondMessage.MessageId);
            secondMessage.Should().BeNull();
            _itemsManager.GetItems().Count.Should().Be(2);
        }

        [Fact]
        public async Task Should_Set_Name()
        {
            await ShowItems();

            var messages = _ctx.Chat.ReadNewBotMessages(_botId, _chatId);
            var secondMessage = messages.Skip(1).First();

            await _ctx.Chat.ClickButton(secondMessage, secondMessage.Buttons!.Skip(1).First().Value);
            var confirmation = _ctx.Chat.ReadNewBotMessages(_botId, _chatId).Single();

            await _ctx.Chat.ClickButton(secondMessage, confirmation.Buttons!.First(b => b.Key == "Yes").Value);
        }

        private async Task ShowItems()
        {
            foreach (var item in _itemsManager.GetItems())
                await ShowItem(item);
        }

        private async Task ShowItem(Item item)
        {
            var context = await _ctx.UserContextRepository.GetUserOrCreateContext(_botId, _chatId, "Boris Sotsky", "esolCrusador");

            await _ctx.StateMachine.StartHandleWorkflow(new ItemWithMessage(item, null), context,
                await _ctx.WorkflowResolver.GetWorkflow<ItemWithMessage>(TaskActionsWorkflowFactory.Id)
            );
        }

        private async Task HandleButtonClick(BotFrameworkButtonClick buttonClick)
        {
            if (buttonClick.SelectedValue.StartsWith("s:"))
            {
                var context = await _ctx.UserContextRepository.GetUserContext(buttonClick.BotId, buttonClick.ChatId);
                var stateString = buttonClick.SelectedValue.Substring(2, buttonClick.SelectedValue.LastIndexOf('-') - 2);
                var state = _ctx.StateMachine.ParseSessionState(context, stateString);
                _ctx.StateMachine.ForceAddEvent(state, buttonClick);

                await _ctx.StateMachine.HandleWorkflow(state, await _ctx.WorkflowResolver.GetWorkflow(state.WorkflowId));
            }
            else
            {
                await _ctx.WorkflowManager.HandleEvent(buttonClick);
            }
        }

        // https://www.figma.com/file/JXTrJQklRBTbbGbvhI0taD/Task-Actions-Dialog?node-id=3%3A76
        class TaskActionsWorkflowFactory : Workflow<ItemWithMessage>
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

            public override IFlow<Unit> Execute(IFlow<ItemWithMessage> input)
            {
                var context = input.Scope.GetContext<UserContext>();

                return input
                    .Persist("Item")
                    .Select((itemWithMessage, scope) =>
                    {
                        var item = itemWithMessage.Item;
                        return scope.StartFlow(() =>
                        {
                            var currentState = scope.GetStateString();

                            return _chatFake.AddOrUpdateBotMessage(context.BotId, context.ChatId, itemWithMessage.MessageId, $"{item.Name}",
                                    new KeyValuePair<string, string>(item.Status == ItemStatus.ToDo ? "Play" : "Pause", $"s:{currentState}-{(item.Status == ItemStatus.ToDo ? "pl" : "pa")}"),
                                    new KeyValuePair<string, string>("Edit", $"s:{currentState}-e"),
                                    new KeyValuePair<string, string>("Delete", $"s:{currentState}-d")
                                );
                        }).PersistBeforePrevious("InitialDialog")
                        .StopAndWait().For<BotFrameworkButtonClick>("InitialButtonClock", messageId => new BotFrameworkButtonClickAwaiter(context, messageId))
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
                                    await _workflowManagerAccessor.WorkflowManager.Start(context, new ItemWithMessage(item, buttonClick.MessageId)).Workflow<EditItemWorkflowFactory>();
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
                        });
                    });
            }

            private async Task UpdateItemMessage(ItemWithMessage item, UserContext dialogContext)
            {
                await _stateMachine.StartHandleWorkflow(item, dialogContext, this);
            }
        }

        class EditItemWorkflowFactory : Workflow<ItemWithMessage>
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

            public override IFlow<Unit> Execute(IFlow<ItemWithMessage> input)
            {
                var context = input.Scope.GetContext<UserContext>();
                
                return input.Persist("Item").Select((item, scope) =>
                {
                    return scope.StartFlow(() => _botFake.SendButtonsBotMessage(context.BotId, context.ChatId, "Do you want to change name?", item.MessageId,
                        new KeyValuePair<string, string>("Yes", "yes"),
                        new KeyValuePair<string, string>("No", "no")
                    ))
                    .Persist("ChangeNameConfirmation")
                    .PersistDisposableItem()
                    .Select(messageId =>
                        scope.StopAndWait<BotFrameworkButtonClick>("ChangeNameConfirmationWait", new BotFrameworkButtonClickAwaiter(context, messageId))
                        .Select(click => click.SelectedValue == "yes")
                        .FinallyAsync(async (isExecuted, _, ex) =>
                        {
                            if (isExecuted)
                                await _botFake.DeleteBotMessage(context.BotId, context.ChatId, messageId);
                        })
                    )
                    .SelectAsync(async (changeName, scope) =>
                    {
                        if (!changeName)
                            return scope.StartFlow();

                        return UpdateItemName(item.Item, await scope.BeginRecursiveScope("Name"));
                    })
                    .SelectAsync(async (_, scope) => await scope.DeleteDisposableItems());
                });
            }

            private IFlow<Unit> UpdateItemName(Item item, StateMachineScope outerScope)
            {
                return outerScope.StartFlow()
                    .Select((_, scope) =>
                    {
                        var context = scope.GetContext<UserContext>();

                        return scope.StartFlow(() =>
                            _botFake.SendBotMessage(context.BotId, context.ChatId, "Please enter name"))
                                .PersistDisposableItem()
                                .Select(messageId =>
                                    scope.StopAndWait<BotFrameworkMessage>("NameInput", new BotFrameworkMessageAwaiter(context))
                                    .Select((nameInput, scope) =>
                                    {
                                        if (string.IsNullOrEmpty(nameInput.Text))
                                            return scope.StartFlow(() => _botFake.SendBotMessage(context.BotId, context.ChatId, "Name is not valid", messageId))
                                                .PersistDisposableItem()
                                                .SelectAsync(async _ =>
                                                        UpdateItemName(item, await scope.IncreaseRecursionDepth())
                                                );

                                        return scope.StartFlow(() => _itemsManager.UpdateItem(item.Id, i => i.Name = nameInput.Text))
                                            .Select(_ => Unit.Default);
                                    })
                                    .DeleteMssages(_botFake)
                                );
                    });
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
            public string? Name { get; set; }
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
