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

        public IObservable<BotFrameworkMessage> UserMessages => _userMessagesSubject;

        public void Dispose()
        {
            _userMessagesSubject.OnCompleted();
            _userMessagesSubject.Dispose();
        }

        public IEnumerable<string> ReadNewBotMessages(Guid userId)
        {
            var lastReadMessage = _lastReadMessage.GetOrAdd(userId, 0);
            var messages = _botMessages.GetOrAdd(userId, _ => new List<BotFrameworkMessage>());
            var result = messages.Skip(lastReadMessage);
            _lastReadMessage.TryUpdate(userId, messages.Count, lastReadMessage);

            return result.Select(m => m.Text);
        }

        public Task<int> SendBotMessage(Guid userId, string message)
        {
            var messages = _botMessages.GetOrAdd(userId, _ => new List<BotFrameworkMessage>());
            int messageId = messages.LastOrDefault()?.MessageId ?? 0 + 1;
            messages.Add(new BotFrameworkMessage(messageId, userId, message));

            return Task.FromResult(messageId);
        }

        public Task<int> SendUserMessage(Guid userId, string message)
        {
            var messages = _userMessages.GetOrAdd(userId, _ => new List<BotFrameworkMessage>());
            int messageId = messages.LastOrDefault()?.MessageId ?? 0 + 1;
            _userMessagesSubject.OnNext(new BotFrameworkMessage(messageId, userId, message));

            return Task.FromResult(messageId);
        }
    }
}
