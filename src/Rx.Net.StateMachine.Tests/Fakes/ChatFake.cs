using Rx.Net.StateMachine.ObservableExtensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Tests.Fakes
{
    public class ChatFake : IDisposable
    {
        private Dictionary<long, UserInfo> _users { get; set; } = new();
        private Bots _bots = new();


        class BotMessages
        {
            public ConcurrentDictionary<long, List<BotFrameworkMessage>> ChatMessages { get; } = new();
            public ConcurrentDictionary<long, int> LastMessageIds = new();
            public ConcurrentDictionary<long, int> LastReadMessageId = new();
        }

        class Bots
        {
            private readonly ConcurrentDictionary<long, BotMessages> _botMessages = new();
            public BotMessages GetMessages(long botId) => _botMessages.GetOrAdd(botId, _ => new BotMessages());

            public IEnumerable<long> GetBotIds() => _botMessages.Keys;
            public int GetLastMessageId(long botId, long chatId) => GetMessages(botId).LastMessageIds.GetOrAdd(chatId, 0);
            public List<BotFrameworkMessage> GetUserMessages(long botId, long chatId) => GetMessages(botId).ChatMessages.GetOrAdd(chatId, _ => new List<BotFrameworkMessage>());
            public int GetLastReadMessageId(long botId, long chatId) => GetMessages(botId).LastReadMessageId.GetOrAdd(chatId, 0);
            public void UpdateLastReadMessageId(long botId, long chatId, int newValue, int previousValue) =>
                GetMessages(botId).LastReadMessageId.TryUpdate(chatId, newValue, previousValue);
        }

        private readonly Subject<BotFrameworkMessage> _userMessages = new();
        private readonly Subject<BotFrameworkMessage> _userMessageHandled = new();
        private readonly Subject<BotFrameworkButtonClick> _buttonClicks = new();
        private readonly Subject<BotFrameworkButtonClick> _buttonClickHandled = new();

        public HandlerRegistration AddClickHandler(Func<BotFrameworkButtonClick, Task> handleClick)
        {
            IObservable<BotFrameworkButtonClick> buttonClicks = _buttonClicks;
            var sub = buttonClicks.SelectAsync(async click =>
            {
                await handleClick(click);
                _buttonClickHandled.OnNext(click);
            }).Merge().Subscribe();

            return new HandlerRegistration(sub);
        }

        public HandlerRegistration AddMessageHandler(Func<BotFrameworkMessage, Task> handleMessage)
        {
            var sub = _userMessages.SelectAsync(async message =>
            {
                await handleMessage(message);
                _userMessageHandled.OnNext(message);
            }).Merge().Subscribe();

            return new HandlerRegistration(sub);
        }

        public void Dispose()
        {
            _userMessages.Dispose();
            _userMessageHandled.Dispose();
            _buttonClickHandled.Dispose();
            _buttonClicks.Dispose();
        }

        public Task<long> RegisterUser(UserInfo user)
        {
            user.UserId = new Random().NextInt64(long.MaxValue);
            _users[user.UserId] = user;

            return Task.FromResult(user.UserId);
        }

        public IReadOnlyCollection<string> ReadNewBotMessageTexts(long botId, long chatId)
        {
            return ReadNewBotMessages(botId, chatId).Select(m => m.Text).ToList();
        }

        public IReadOnlyCollection<string> ReadAllMessageTexts(long botId, long chatId)
        {
            return _bots.GetUserMessages(botId, chatId).Select(m => m.Text).ToList();
        }

        public IReadOnlyCollection<BotFrameworkMessage> ReadNewBotMessages(long botId, long chatId)
        {
            var lastReadMessageId = _bots.GetLastReadMessageId(botId, chatId);
            var messages = _bots.GetUserMessages(botId, chatId);
            var result = messages.Where(m => m.MessageId > lastReadMessageId).ToList();
            _bots.UpdateLastReadMessageId(botId, chatId, messages.Select(m => m.MessageId).DefaultIfEmpty(lastReadMessageId).Max(), lastReadMessageId);

            return result.Where(m => m.Source == MessageSource.Bot).ToList();
        }

        public BotFrameworkMessage? ReadMessage(long botId, long chatId, int messageId)
        {
            return GetUserMessages(botId, chatId).Find(m => m.MessageId == messageId);
        }

        public Task<int> SendBotMessage(long botId, long chatId, string message, int? replyToMessageId = default)
        {
            var messageId = GetNextBotMessageId(botId, chatId);
            return SendBotMessage(new BotFrameworkMessage(messageId, botId, chatId, MessageSource.Bot, message, _users[chatId])
            {
                ReplyToMessageId = replyToMessageId
            });
        }

        public Task<int> SendButtonsBotMessage(long botId, long chatId, string message, params KeyValuePair<string, string>[] buttons)
        {
            return SendButtonsBotMessage(botId, chatId, message, null, buttons);
        }

        public Task<int> SendButtonsBotMessage(long botId, long chatId, string message, int? replyToMessageId, params KeyValuePair<string, string>[] buttons)
        {
            var messageId = GetNextBotMessageId(botId, chatId);
            return SendBotMessage(new BotFrameworkMessage(messageId, botId, chatId, MessageSource.Bot, message, _users[chatId])
            {
                Buttons = buttons,
                ReplyToMessageId = replyToMessageId
            });
        }

        public Task UpdateBotMessage(long botId, long chatId, int messageId, string message, params KeyValuePair<string, string>[] buttons)
        {
            var messageIndex = GetUserMessages(botId, chatId).FindIndex(m => m.MessageId == messageId);
            GetUserMessages(botId, chatId)[messageIndex] = new BotFrameworkMessage(messageId, botId, chatId, MessageSource.Bot, message, _users[chatId])
            {
                Buttons = buttons.Length == 0 ? null : buttons,
                ReplyToMessageId = GetUserMessages(botId, chatId)[messageIndex].ReplyToMessageId
            };

            return Task.CompletedTask;
        }

        public Task<int> AddOrUpdateBotMessage(long botId, long chatId, int? messageId, string message, params KeyValuePair<string, string>[] buttons)
        {
            if (messageId.HasValue)
            {
                UpdateBotMessage(botId, chatId, messageId.Value, message, buttons);
                return Task.FromResult(messageId.Value);
            }

            if (buttons.Length == 0)
                return SendBotMessage(botId, chatId, message);

            return SendButtonsBotMessage(botId, chatId, message, buttons);
        }

        public async Task<int> SendUserMessage(long botId, long chatId, string messageText, int handledCount = 1)
        {
            var messages = GetUserMessages(botId, chatId);
            int messageId = GetNextBotMessageId(botId, chatId);
            var message = new BotFrameworkMessage(messageId, botId, chatId, MessageSource.User, messageText, _users[chatId]);
            messages.Add(message);
            var handled = _userMessageHandled.Where(m => m == message).Take(handledCount).Timeout(TimeSpan.FromSeconds(10)).ToTask();
            _userMessages.OnNext(message);
            await handled;

            return messageId;
        }

        public async Task ClickButton(BotFrameworkMessage message, string buttonValue, int handledTimes = 1)
        {
            var click = new BotFrameworkButtonClick { BotId = message.BotId, ChatId = message.ChatId, MessageId = message.MessageId, SelectedValue = buttonValue };
            var handled = _buttonClickHandled.Tap(cl => Debugger.Break()).Where(cl => cl == click).Take(handledTimes).Timeout(TimeSpan.FromSeconds(10)).ToTask();
            _buttonClicks.OnNext(click);

            await handled;
        }

        public async Task ClickButtonAndWaitUntilHandled(BotFrameworkMessage message, string buttonValue)
        {
            await ClickButton(message, buttonValue);
        }

        public Task DeleteBotMessage(long botId, long chatId, int messageId)
        {
            var messages = GetUserMessages(botId, chatId);
            messages.RemoveAt(messages.FindIndex(m => m.MessageId == messageId));

            return Task.CompletedTask;
        }

        public Task DeleteUserMessage(long botId, long chatId, int messageId)
        {
            var messages = GetUserMessages(botId, chatId);
            messages.RemoveAt(messages.FindIndex(m => m.MessageId == messageId));

            return Task.CompletedTask;
        }


        private int GetNextBotMessageId(long botId, long chatId)
        {
            int nextMessageId = 1;
            _bots.GetMessages(botId).LastMessageIds.AddOrUpdate(chatId, nextMessageId, (_, previusMessageId) => nextMessageId = previusMessageId + 1);

            return nextMessageId;
        }

        private Task<int> SendBotMessage(BotFrameworkMessage message)
        {
            GetUserMessages(message.BotId, message.ChatId).Add(message);

            return Task.FromResult(message.MessageId);
        }

        private List<BotFrameworkMessage> GetUserMessages(long botId, long chatId) =>
            _bots.GetUserMessages(botId, chatId);

        public string MessagesLog
        {
            get
            {
                var bots = _bots.GetBotIds().Select(botId =>
                {
                    var chats = _bots.GetMessages(botId).ChatMessages.Select(m =>
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

                    return $"Bot-{botId}:\r\n{string.Join(";\r\n", chats)}";
                });

                return string.Join("-------\r\n", bots);
            }
        }

        public override string ToString() => MessagesLog;
    }
}
