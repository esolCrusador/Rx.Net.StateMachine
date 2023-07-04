using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Rx.Net.StateMachine.ObservableExtensions;

namespace Rx.Net.StateMachine.Tests.DataAccess
{
    public class MessageQueue : IDisposable
    {
        private readonly Subject<object> _events = new Subject<object>();
        private readonly Subject<object> _handled = new Subject<object>();

        public void Dispose() => _events.Dispose();

        public Task Send(object ev)
        {
            _events.OnNext(ev);
            return Task.CompletedTask;
        }

        public async Task SendAndWait(object ev, int times = 1)
        {
            var handled = _handled.Where(e => ev == e).Take(times).ToTask();
            await Send(ev);
            await handled;
        }

        public async Task WaitUntilHandled(Func<object, bool> eventFilter, int times = 1)
        {
            await _handled.Where(eventFilter).Take(times).ToTask();
        }

        public IDisposable AddEventHandler<TEvent>(Func<TEvent, Task> handler)
        {
            return _events.Where(ev => ev is TEvent).Select(ev =>
            {
                return Observable.FromAsync(async () =>
                {
                    await handler((TEvent)ev);
                    _handled.OnNext(ev);
                });
            }).Merge().Subscribe();
        }
    }
}
