using Rx.Net.StateMachine.Tests.Persistence;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Tests.Fakes
{
    public class BotFake : IDisposable
    {
        private readonly ConcurrentDictionary<Guid, List<BotFrameworkMessage>> _messages = new ConcurrentDictionary<Guid, List<BotFrameworkMessage>>();
        private readonly ConcurrentDictionary<Guid, int> _lastMessageIds = new ConcurrentDictionary<Guid, int>();
        private readonly ConcurrentDictionary<Guid, int> _lastReadMessageIds = new ConcurrentDictionary<Guid, int>();

        private readonly Subject<BotFrameworkMessage> _userMessagesSubject = new Subject<BotFrameworkMessage>();
        private readonly Subject<BotFrameworkButtonClick> _buttonClicks = new Subject<BotFrameworkButtonClick>();

        public IObservable<BotFrameworkMessage> UserMessages => _userMessagesSubject;
        public IObservable<BotFrameworkButtonClick> ButtonClick => _buttonClicks;

        public void Dispose()
        {
            _userMessagesSubject.OnCompleted();
            _userMessagesSubject.Dispose();
        }

        public IReadOnlyCollection<string> ReadNewBotMessageTexts(Guid userId)
        {
            return ReadNewBotMessages(userId).Select(m => m.Text).ToList();
        }

        public IReadOnlyCollection<BotFrameworkMessage> ReadNewBotMessages(Guid userId)
        {
            var lastReadMessageId = _lastReadMessageIds.GetOrAdd(userId, 0);
            var messages = GetUserMessages(userId);
            var result = messages.Where(m => m.MessageId > lastReadMessageId).ToList();
            _lastReadMessageIds.TryUpdate(userId,
                messages.Select(m => m.MessageId).DefaultIfEmpty(lastReadMessageId).Max(),
                lastReadMessageId
            );

            return result.Where(m => m.Source == MessageSource.Bot).ToList();
        }

        public BotFrameworkMessage ReadMessage(Guid userId, int messageId)
        {
            return GetUserMessages(userId).Find(m => m.MessageId == messageId);
        }

        public Task<int> SendBotMessage(Guid userId, string message, int? replyToMessageId = default)
        {
            var messageId = GetNextBotMessageId(userId);
            return SendBotMessage(userId, new BotFrameworkMessage(messageId, userId, MessageSource.Bot, message)
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
            return SendBotMessage(userId, new BotFrameworkMessage(messageId, userId, MessageSource.Bot, message)
            {
                Buttons = buttons,
                ReplyToMessageId = replyToMessageId
            });
        }

        public Task UpdateBotMessage(Guid userId, int messageId, string message, params KeyValuePair<string, string>[] buttons)
        {
            var messageIndex = GetUserMessages(userId).FindIndex(m => m.MessageId == messageId);
            GetUserMessages(userId)[messageIndex] = new BotFrameworkMessage(messageId, userId, MessageSource.Bot, message)
            {
                Buttons = buttons.Length == 0 ? null : buttons,
                ReplyToMessageId = GetUserMessages(userId)[messageIndex].ReplyToMessageId
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

        public Task<int> SendUserMessage(Guid userId, string messageText)
        {
            var messages = GetUserMessages(userId);
            int messageId = messages.LastOrDefault()?.MessageId ?? 0 + 1;
            var message = new BotFrameworkMessage(messageId, userId, MessageSource.User, messageText);
            messages.Add(message);
            _userMessagesSubject.OnNext(message);

            return Task.FromResult(messageId);
        }

        public Task ClickButton(BotFrameworkMessage message, string buttonValue)
        {
            _buttonClicks.OnNext(new BotFrameworkButtonClick { UserId = message.UserId, MessageId = message.MessageId, SelectedValue = buttonValue });
            return Task.CompletedTask;
        }

        public Task DeleteBotMessage(Guid userId, int messageId)
        {
            var messages = GetUserMessages(userId);
            messages.RemoveAt(messages.FindIndex(m => m.MessageId == messageId));
            if (_lastReadMessageIds.TryGetValue(userId, out int messageCounter))
                _lastReadMessageIds[userId] = messageCounter - 1;

            return Task.CompletedTask;
        }

        public Task DeleteUserMessage(Guid userId, int messageId)
        {
            var messages = GetUserMessages(userId);
            messages.RemoveAt(messages.FindIndex(m => m.MessageId == messageId));

            return Task.CompletedTask;
        }


        private int GetNextBotMessageId(Guid userId)
        {
            int nextMessageId = 1;
            _lastMessageIds.AddOrUpdate(userId, nextMessageId, (_, previusMessageId) => nextMessageId = previusMessageId + 1);

            return nextMessageId;
        }

        private Task<int> SendBotMessage(Guid userId, BotFrameworkMessage message)
        {
            GetUserMessages(userId).Add(message);

            return Task.FromResult(message.MessageId);
        }

        private List<BotFrameworkMessage> GetUserMessages(Guid userId) =>
            _messages.GetOrAdd(userId, new List<BotFrameworkMessage>());

        public override string ToString()
        {
            var chats = _messages.Select(m =>
            {
                var chatMessages = m.Value.Select(m =>
                {
                    string message = $"[{m.MessageId}] {(m.Source == MessageSource.User ? "User" : "Bot")}: {m.Text}";
                    if (m.Buttons != null && m.Buttons.Count > 0)
                        message += $"\r\n{string.Join(", ", m.Buttons.Select(b => $"[{b.Key}]"))}";

                    return message;
                });

                return $"{m.Key}:\r\n{string.Join("\r\n", chatMessages)}";
            });

            return string.Join(";\r\n", chats);
        }
    }
}
