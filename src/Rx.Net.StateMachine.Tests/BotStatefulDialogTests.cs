using FluentAssertions;
using Rx.Net.StateMachine.ObservableExtensions;
using Rx.Net.StateMachine.States;
using Rx.Net.StateMachine.Tests.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Rx.Net.StateMachine.Tests
{
    public class BotStatefulDialogTests : IDisposable
    {
        private IDisposable _buttonClickSubscription;
        private readonly StateMachine _stateMachine = new StateMachine();
        private readonly Guid _userId = Guid.NewGuid();
        private List<Item> _sampleItems;
        private readonly BotFake _botFake;

        public BotStatefulDialogTests()
        {
            _botFake = new BotFake();
            _sampleItems = new List<Item>
            {
                new Item{Id = Guid.NewGuid(), Name = "Task 1", Status = ItemStatus.ToDo},
                new Item{Id = Guid.NewGuid(), Name = "Task 2", Status = ItemStatus.ToDo},
                new Item{Id = Guid.NewGuid(), Name = "Task 3", Status = ItemStatus.InProgress}
            };
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
            var messages = _botFake.ReadNewMessages(_userId);
            var secondMessage = messages.Skip(1).First();

            await _botFake.ClickButton(secondMessage, secondMessage.Buttons.First().Value);
            var newMessages = _botFake.ReadNewBotMessageTexts(_userId);
            newMessages.Should().BeEquivalentTo(new[] { "Play" });
        }

        private async Task ShowItems()
        {
            foreach (var item in _sampleItems)
            {
                await ShowItem(item);
            }
        }

        private async Task ShowItem(Item item)
        {
            var context = new DialogContext { UserId = _userId };

            await _stateMachine.StartHandleWorkflow(context, scope => StartDialog(StateMachineObservableExtensions.Of(item), scope));
        }

        private async Task HandleButtonClick(BotFrameworkButtonClick buttonClick)
        {
            if (buttonClick.SelectedValue.StartsWith("s:"))
            {
                var context = new DialogContext { UserId = buttonClick.UserId, MessageId = buttonClick.MessageId };
                var stateString = buttonClick.SelectedValue.Substring(2, buttonClick.SelectedValue.LastIndexOf('-') - 2);
                var state = _stateMachine.ParseSessionState(context, stateString);
                _stateMachine.ForceAddEvent(state, buttonClick);

                await _stateMachine.HandleWorkflow(state, scope => StartDialog(Observable.Empty<Item>(), scope));
            }
        }

        // https://www.figma.com/file/JXTrJQklRBTbbGbvhI0taD/Task-Actions-Dialog?node-id=3%3A76
        private IObservable<Unit> StartDialog(IObservable<Item> item, StateMachineScope scope)
        {
            var context = scope.GetContext<DialogContext>();

            return item
                .Persist(scope, "Item")
                .Select(item =>
                {
                    return Observable.FromAsync(() =>
                    {
                        var currentState = scope.GetStateString();

                        return _botFake.SendButtonsBotMessage(context.UserId, $"{item.Name}",
                            new KeyValuePair<string, string>(item.Status == ItemStatus.ToDo ? "Play" : "Pause", $"s:{currentState}-{(item.Status == ItemStatus.ToDo ? "pl" : "pa")}"),
                            new KeyValuePair<string, string>("Edit", $"s:{currentState}-e"),
                            new KeyValuePair<string, string>("Delete", $"s:{currentState}-d")
                        );
                    }).PersistBeforePrevious(scope, "InitialDialog")
                    .Select(_ => context.MessageId);
                })
                .Concat()
                .StopAndWait().For<BotFrameworkButtonClick>(scope, "InitialButtonClock")
                .SelectAsync(buttonClick =>
                {
                    string selectedValue = buttonClick.SelectedValue.Substring(buttonClick.SelectedValue.LastIndexOf('-') + 1);
                    switch (selectedValue)
                    {
                        case "pl":
                            return _botFake.SendBotMessage(context.UserId, "Play");
                        case "pa":
                            return _botFake.SendBotMessage(context.UserId, "Pause");
                        case "e":
                            return _botFake.SendBotMessage(context.UserId, "Edit");
                        case "d":
                            return _botFake.SendBotMessage(context.UserId, "Delete");
                        default:
                            throw new NotSupportedException(buttonClick.SelectedValue);
                    }
                })
                .Concat()
                .Select(_ => Unit.Default);
        }

        class DialogContext
        {
            public Guid UserId { get; set; }
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
