using System;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Tests.DataAccess
{
    public class MessageQueue : IDisposable
    {
        private readonly Subject<object> _events = new Subject<object>();
        private readonly Subject<object> _handled = new Subject<object>();
        private readonly Subject<(Exception Exception, object Target)> _failed = new();

        public void Dispose() => _events.Dispose();

        public Task Send(object ev)
        {
            _events.OnNext(ev);
            return Task.CompletedTask;
        }

        public async Task SendAndWait(object ev, int times = 1)
        {
            using CancellationTokenSource cancellation = new CancellationTokenSource();

            var handled = _handled.Where(e => ev == e).Take(times).ToTask(cancellation.Token);
            var failed = _failed.Where(f => f.Target == ev).Take(times).ToTask(cancellation.Token);

            await Send(ev);

            var task = await Task.WhenAny(handled, failed);
            cancellation.Cancel();
            if (task == failed)
                throw (await failed).Exception;
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
                    try
                    {
                        await handler((TEvent)ev);
                        _handled.OnNext(ev);
                    }
                    catch(Exception ex)
                    {
                        _failed.OnNext((ex, ev));
                    }
                });
            }).Merge().Subscribe();
        }
    }
}
