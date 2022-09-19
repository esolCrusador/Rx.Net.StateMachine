using Rx.Net.StateMachine.Tests.Persistence;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Tests
{
    public class BotFake : IDisposable
    {
        private readonly ConcurrentDictionary<Guid, List<BotFrameworkMessage>> _userMessages = new ConcurrentDictionary<Guid, List<BotFrameworkMessage>>();
        private readonly ConcurrentDictionary<Guid, List<BotFrameworkMessage>> _botMessages = new ConcurrentDictionary<Guid, List<BotFrameworkMessage>>();

        private ConcurrentDictionary<Guid, int> _lastReadMessage = new ConcurrentDictionary<Guid, int>();
        private Subject<BotFrameworkMessage> _userMessagesSubject = new Subject<BotFrameworkMessage>();
        private Subject<BotFrameworkMessage> _botMessagesSubject = new Subject<BotFrameworkMessage>();
        private Subject<BotFrameworkButtonClick> _buttonClicks = new Subject<BotFrameworkButtonClick>();

        public IObservable<BotFrameworkMessage> UserMessages => _userMessagesSubject;

        public IObservable<BotFrameworkButtonClick> ButtonClick => _buttonClicks;

        public void Dispose()
        {
            _userMessagesSubject.OnCompleted();
            _userMessagesSubject.Dispose();
        }

        public IReadOnlyCollection<string> ReadNewBotMessageTexts(Guid userId)
        {
            return ReadNewMessages(userId).Select(m => m.Text).ToList();
        }

        public IReadOnlyCollection<BotFrameworkMessage> ReadNewMessages(Guid userId)
        {
            var lastReadMessage = _lastReadMessage.GetOrAdd(userId, 0);
            var messages = _botMessages.GetOrAdd(userId, _ => new List<BotFrameworkMessage>());
            var result = messages.Skip(lastReadMessage).ToList();
            _lastReadMessage.TryUpdate(userId, messages.Count, lastReadMessage);

            return result;
        } 

        public Task<int> SendBotMessage(Guid userId, string message)
        {
            var messageId = GetNextBotMessageId(userId);
            return SendBotMessage(userId, new BotFrameworkMessage(messageId, userId, message));
        }

        public Task <int> SendButtonsBotMessage(Guid userId, string message, params KeyValuePair<string, string>[] buttons)
        {
            var messageId = GetNextBotMessageId(userId);
            return SendBotMessage(userId, new BotFrameworkMessage(messageId, userId, message)
            {
                Buttons = buttons
            });
        }

        public Task<int> SendUserMessage(Guid userId, string message)
        {
            var messages = _userMessages.GetOrAdd(userId, _ => new List<BotFrameworkMessage>());
            int messageId = messages.LastOrDefault()?.MessageId ?? 0 + 1;
            _userMessagesSubject.OnNext(new BotFrameworkMessage(messageId, userId, message));

            return Task.FromResult(messageId);
        }

        public Task ClickButton(BotFrameworkMessage message, string buttonValue)
        {
            _buttonClicks.OnNext(new BotFrameworkButtonClick { UserId = message.UserId, MessageId = message.MessageId, SelectedValue = buttonValue });
            return Task.CompletedTask;
        }

        private int GetNextBotMessageId(Guid userId)
        {
            var messages = _botMessages.GetOrAdd(userId, _ => new List<BotFrameworkMessage>());
            return messages.LastOrDefault()?.MessageId ?? 0 + 1;
        }

        private Task<int> SendBotMessage(Guid userId, BotFrameworkMessage message)
        {
            _botMessages[userId].Add(message);

            return Task.FromResult(message.MessageId);
        }
    }
}
