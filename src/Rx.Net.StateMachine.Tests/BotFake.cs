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

        public BotFrameworkMessage ReadMessage(Guid userId, int messageId)
        {
            return _botMessages[userId].Find(m => m.MessageId == messageId);
        }

        public Task<int> SendBotMessage(Guid userId, string message, int? replyToMessageId = default)
        {
            var messageId = GetNextBotMessageId(userId);
            return SendBotMessage(userId, new BotFrameworkMessage(messageId, userId, message)
            {
                ReplyToMessageId = replyToMessageId
            });
        }

        public Task<int> SendButtonsBotMessage(Guid userId, string message, params KeyValuePair<string, string>[] buttons)
        {
            return SendButtonsBotMessage(userId, message, null, buttons);
        }

        public Task<int> SendButtonsBotMessage(Guid userId, string message, int? replyToMessageId, params KeyValuePair<string, string>[] buttons)
        {
            var messageId = GetNextBotMessageId(userId);
            return SendBotMessage(userId, new BotFrameworkMessage(messageId, userId, message)
            {
                Buttons = buttons,
                ReplyToMessageId = replyToMessageId
            });
        }

        public Task UpdateBotMessage(Guid userId, int messageId, string message, params KeyValuePair<string, string>[] buttons)
        {
            var messageIndex = _botMessages[userId].FindIndex(m => m.MessageId == messageId);
            _botMessages[userId][messageIndex] = new BotFrameworkMessage(messageId, userId, message)
            {
                Buttons = buttons.Length == 0 ? null : buttons,
                ReplyToMessageId = _botMessages[userId][messageIndex].ReplyToMessageId
            };

            return Task.CompletedTask;
        }

        public Task<int> AddOrUpdateBotMessage(Guid userId, int? messageId, string message, params KeyValuePair<string, string>[] buttons)
        {
            if (messageId.HasValue)
            {
                UpdateBotMessage(userId, messageId.Value, message, buttons);
                return Task.FromResult(messageId.Value);
            }

            if (buttons.Length == 0)
                return SendBotMessage(userId, message);

            return SendButtonsBotMessage(userId, message, buttons);
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

        public Task DeleteBotMessage(Guid userId, int messageId)
        {
            var messages = _botMessages[userId];
            messages.RemoveAt(messages.FindIndex(m => m.MessageId == messageId));
            if (_lastReadMessage.TryGetValue(userId, out int messageCounter))
                _lastReadMessage[userId] = messageCounter - 1;

            return Task.CompletedTask;
        }

        public Task DeleteUserMessage(Guid userId, int messageId)
        {
            var messages = _userMessages[userId];
            messages.RemoveAt(messages.FindIndex(m => m.MessageId == messageId));

            return Task.CompletedTask;
        }

        private int GetNextBotMessageId(Guid userId)
        {
            var messages = _botMessages.GetOrAdd(userId, _ => new List<BotFrameworkMessage>());
            return (messages.LastOrDefault()?.MessageId ?? 0) + 1;
        }

        private Task<int> SendBotMessage(Guid userId, BotFrameworkMessage message)
        {
            _botMessages[userId].Add(message);

            return Task.FromResult(message.MessageId);
        }
    }
}
