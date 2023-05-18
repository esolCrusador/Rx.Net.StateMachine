using System;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Tests.Testing
{
    public class AsyncWait
    {
        private readonly TimeSpan _defaultDelay;

        public AsyncWait(TimeSpan defaultDelay)
        {
            _defaultDelay = defaultDelay;
        }

        public async Task For(Func<Task> result, TimeSpan frame = default)
        {
            if(frame == default)
                frame = _defaultDelay;

            DateTimeOffset started = DateTimeOffset.UtcNow;

            while (true)
                try
                {
                    await result();
                    return;
                }
                catch
                {
                    if (DateTimeOffset.UtcNow - started > frame)
                        throw;

                    await Task.Delay(frame / 50);
                }
        }

        public async Task For(Action result, TimeSpan frame = default)
        {
            if (frame == default)
                frame = _defaultDelay;
            DateTimeOffset started = DateTimeOffset.UtcNow;

            while (true)
                try
                {
                    result();
                    return;
                }
                catch
                {
                    if (DateTimeOffset.UtcNow - started > frame)
                        throw;

                    await Task.Delay(frame / 50);
                }
        }
    }
}
