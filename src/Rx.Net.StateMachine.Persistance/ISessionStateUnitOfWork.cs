using Rx.Net.StateMachine.Persistance.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Persistance
{
    public interface ISessionStateUnitOfWork : IDisposable, IAsyncDisposable
    {
        Task<IReadOnlyCollection<ISessionStateMemento>> GetSessionStates(object @event, CancellationToken cancellationToken);
        Task<IReadOnlyCollection<ISessionStateMemento>> GetSessionStates(IEnumerable<object> events, CancellationToken cancellationToken);
        Task<ISessionStateMemento?> GetSessionState(Guid sessionStateId, CancellationToken cancellationToken);
        ISessionStateMemento Add(SessionStateEntity sessionState);
    }
}
