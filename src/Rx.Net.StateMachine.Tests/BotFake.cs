using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Tests
{
    public class BotFake : IDisposable
    {
        private int _messageId = 0;
        private readonly List<string> _userMessages = new List<string>();
        private readonly List<string> _botMessages = new List<string>();

        private int _lastReadMessage = 0;
        private Subject<string> _userMessagesSubject = new Subject<string>();

        public IObservable<string> UserMessages => _userMessagesSubject;

        public void Dispose()
        {
            _userMessagesSubject.OnCompleted();
            _userMessagesSubject.Dispose();
        }

        public IEnumerable<string> ReadNewBotMessages()
        {
            var result = _botMessages.Skip(_lastReadMessage);
            _lastReadMessage = _botMessages.Count;

            return result;
        }

        public Task<int> SendBotMessage(string message)
        {
            _botMessages.Add(message);

            return Task.FromResult(++_messageId);
        }

        public Task<int> SendUserMessage(string message)
        {
            _userMessages.Add(message);
            _userMessagesSubject.OnNext(message);

            return Task.FromResult(++_messageId);
        }
    }
}
