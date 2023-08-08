using Rx.Net.StateMachine.Extensions;
using Rx.Net.StateMachine.Flow;
using Rx.Net.StateMachine.Tests.Awaiters;
using Rx.Net.StateMachine.Tests.Extensions;
using Rx.Net.StateMachine.Tests.Fakes;
using Rx.Net.StateMachine.Tests.Persistence;
using System.Collections.Generic;
using System.Reactive.Linq;

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
    public class ConfirmationControl : IControl<DialogConfiguration, bool>
    {
        private readonly ChatFake _chat;

        public ConfirmationControl(ChatFake chat)
        {
            _chat = chat;
        }

        public IFlow<bool> StartDialog(StateMachineScope scope, DialogConfiguration source)
        {
            var userContext = scope.GetContext<UserContext>();
            return scope.StartFlow(() => _chat.SendButtonsBotMessage(
                userContext.BotId,
                userContext.ChatId,
                source.Message,
                new KeyValuePair<string, string>(
                    new WorkflowCallbackQuery { Command = "confirm", Parameters = { ["value"] = "1" } }.ToString(),
                    source.YesButton
                ),
                new KeyValuePair<string, string>(
                    new WorkflowCallbackQuery { Command = "confirm", Parameters = { ["value"] = "0" } }.ToString(),
                    source.NoButton
                )
            ))
            .PersistDisposableItem()
            .Persist("ConfirmButtonAdded")
            .StopAndWait().For<BotFrameworkButtonClick>("ConfirmButton", messageId => new BotFrameworkButtonClickAwaiter(userContext, messageId), bc =>
            {
                var query = WorkflowCallbackQuery.Parse(bc.SelectedValue);
                return query.Command == "confirm";
            })
            .Select(bc =>
            {
                var query = WorkflowCallbackQuery.Parse(bc.SelectedValue);
                return query.Parameters["value"] == "1";
            })
            .DeleteMssages(_chat)
            .Persist("ConfirmationResult");
        }
    }
}
