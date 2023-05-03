using System;

namespace Rx.Net.StateMachine.Tests.Fakes
{
    public class HandlerRegistration : IDisposable
    {
        private readonly IDisposable _handler;

        public HandlerRegistration(IDisposable handler) => _handler = handler;
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _handler.Dispose();
        }
    }
}
