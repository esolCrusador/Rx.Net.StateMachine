using Rx.Net.StateMachine.ObservableExtensions;
using Rx.Net.StateMachine.Tests.Awaiters;
using Rx.Net.StateMachine.Tests.Extensions;
using Rx.Net.StateMachine.Tests.Fakes;
using Rx.Net.StateMachine.Tests.Persistence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Tests.Controls
{
    public struct DialogConfiguration
    {
        public string Message { get; }
        public string YesButton { get; set; } = "yes";
        public string NoButton { get; set; } = "no";

        public DialogConfiguration(string message)
        {
            Message = message;
        }

    }
    public class ConfirmationControl: IControl<DialogConfiguration, bool>
    {
        private readonly ChatFake _chat;

        public ConfirmationControl(ChatFake chat)
        {
            _chat = chat;
        }

        public IObservable<bool> StartDialog(StateMachineScope scope, DialogConfiguration source)
        {
            var userContext = scope.GetContext<UserContext>();
            return Observable.FromAsync(() => _chat.SendButtonsBotMessage(
                userContext.BotId,
                userContext.ChatId,
                source.Message,
                new KeyValuePair<string, string>(
                    new WorkflowCallbackQuery { SessionId = scope.SessionId, Command = "confirm", Parameters = { ["value"] = "1" } }.ToString(),
                    source.YesButton
                ),
                new KeyValuePair<string, string>(
                    new WorkflowCallbackQuery { SessionId = scope.SessionId, Command = "confirm", Parameters = { ["value"] = "0" } }.ToString(),
                    source.NoButton
                )
            ))
            .PersistMessageId(scope)
            .Persist(scope, "ConfirmButtonAdded")
            .StopAndWait().For<BotFrameworkButtonClick>(scope, "ConfirmButton", messageId => new BotFrameworkButtonClickAwaiter(messageId), bc =>
            {
                var query = WorkflowCallbackQuery.Parse(bc.SelectedValue);
                return query.Command == "confirm";
            })
            .Select(bc =>
            {
                var query = WorkflowCallbackQuery.Parse(bc.SelectedValue);
                return query.Parameters["value"] == "1";
            })
            .DeleteMssages(scope, _chat)
            .Persist(scope, "ConfirmationResult");
        }
    }
}
