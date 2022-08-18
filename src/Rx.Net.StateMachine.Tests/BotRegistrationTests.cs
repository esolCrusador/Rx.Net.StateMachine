using FluentAssertions;
using Rx.Net.StateMachine.ObservableExtensions;
using Rx.Net.StateMachine.States;
using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Rx.Net.StateMachine.Tests
{
    class UserModel
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime BirthDate { get; set; }
    }
    public class UserMessage
    {
        public string Text { get; set; }
    }
    public class BotRegistrationTests
    {
        private readonly SessionStateDataStore _dataStore;
        private readonly WorkflowManager _workflowManager;
        private readonly BotFake _botFake;

        public BotRegistrationTests()
        {
            _botFake = new BotFake();
            _dataStore = new SessionStateDataStore();
            _workflowManager = new WorkflowManager(
                new JsonSerializerOptions(), 
                () => new SessionStateUnitOfWork(_dataStore), 
                ss => scope => GetRegistrationWorkflow(scope)
            );
        }

        [Fact]
        public async Task Should_Return_User_Information()
        {
            var boris = Guid.NewGuid();
            await _workflowManager.HandleEvent(new UserMessage { Text = "/Start" }, boris);

            var botMessages = _botFake.ReadNewBotMessages();
            botMessages.Should().BeEquivalentTo("Hello, please follow steps to pass registration process", "Please enter your first name");

            await _workflowManager.HandleEvent(new UserMessage { Text = "Boris" }, boris);
            botMessages = _botFake.ReadNewBotMessages();
            botMessages.Should().BeEquivalentTo("Please enter your last name");

            await _workflowManager.HandleEvent(new UserMessage { Text = "Sotsky" }, boris);
            botMessages = _botFake.ReadNewBotMessages();
            botMessages.Should().BeEquivalentTo("Please enter your birth date");

            await _workflowManager.HandleEvent(new UserMessage { Text = new DateTime(1987, 6, 23).ToShortDateString() }, boris);
            botMessages = _botFake.ReadNewBotMessages();
            botMessages.Should().HaveCount(1);
        }

        // Workflow: https://www.figma.com/file/WPqeeRL8EjiH1rzXT1os7o/User-Registration-Case?node-id=0%3A1
        private IObservable<Unit> GetRegistrationWorkflow(StateMachineScope scope)
        {
            return Observable.FromAsync(() => _botFake.SendBotMessage("Hello, please follow steps to pass registration process"))
                .Select(_ => new UserModel { Id = Guid.NewGuid() })
                .Persist(scope, "UserId")
                .Select(user => GetFirstName(scope.BeginScope("FirstName")).Select(firstName =>
                {
                    user.FirstName = firstName;
                    return user;
                }))
                .Concat()
                .Persist(scope, "FirstName")
                .Select(user => GetLastName(scope.BeginScope("LastName")).Select(lastName =>
                {
                    user.LastName = lastName;
                    return user;
                }))
                .Concat()
                .Persist(scope, "LastName")
                .Select(user => GetBirthDate(scope.BeginScope("BirthDate")).Select(birthDate =>
                {
                    user.BirthDate = birthDate;
                    return user;
                }))
                .Concat()
                .SelectAsync(async user =>
                {
                    await _botFake.SendBotMessage($"You was successfuly registered: {JsonSerializer.Serialize(user)}");

                    return Unit.Default;
                })
                .Concat();
        }

        private IObservable<string> GetFirstName(StateMachineScope scope, int attempt = 1)
        {
            return Observable.FromAsync(() => _botFake.SendBotMessage("Please enter your first name"))
                .Persist(scope, $"AskUserFirstName-{attempt}")
                .StopAndWait().For<UserMessage>(scope)
                .Select(message =>
                {
                    if (!string.IsNullOrWhiteSpace(message.Text))
                        return StateMachineObservableExtensions.Of(message.Text);

                    return Observable.FromAsync(() => _botFake.SendBotMessage("Oops first name is not valid, please try again"))
                                .Persist(scope, $"InvalidFirstName-{attempt}")
                                .Select(_ => GetFirstName(scope, attempt + 1))
                                .Concat();
                }).Concat();
        }

        private IObservable<string> GetLastName(StateMachineScope scope, int attempt = 1)
        {
            return Observable.FromAsync(() => _botFake.SendBotMessage("Please enter your last name"))
                .Persist(scope, $"AskUserLastName-{attempt}")
                .StopAndWait().For<UserMessage>(scope)
                .Select(message =>
                {
                    if (!string.IsNullOrWhiteSpace(message.Text))
                        return StateMachineObservableExtensions.Of(message.Text);

                    return Observable.FromAsync(() => _botFake.SendBotMessage("Oops last name is not valid, please try again"))
                                .Persist(scope, $"InvalidLastName-{attempt}")
                                .Select(_ => GetLastName(scope, attempt + 1))
                                .Concat();
                }).Concat();
        }

        private IObservable<DateTime> GetBirthDate(StateMachineScope scope, int attempt = 1)
        {
            return Observable.FromAsync(() => _botFake.SendBotMessage("Please enter your birth date"))
                .Persist(scope, $"AskUserBirthDate-{attempt}")
                .StopAndWait().For<UserMessage>(scope)
                .Select(message =>
                {
                    if (!string.IsNullOrWhiteSpace(message.Text) && DateTime.TryParse(message.Text, out var birthDate))
                        return StateMachineObservableExtensions.Of(birthDate);

                    return Observable.FromAsync(() => _botFake.SendBotMessage("Oops birth date is not valid, please try again"))
                                .SelectAsync(_ => _botFake.SendBotMessage($"For example: {DateTime.Now.AddYears(-30).ToShortDateString()}"))
                                .Persist(scope, $"InvalidBirthDate-{attempt}")
                                .Select(_ => GetBirthDate(scope, attempt + 1))
                                .Concat();
                }).Concat();
        }
    }
}
