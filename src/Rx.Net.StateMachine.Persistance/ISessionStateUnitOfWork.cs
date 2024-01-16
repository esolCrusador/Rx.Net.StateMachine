using Rx.Net.StateMachine.Persistance.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Persistance
{
    public interface ISessionStateUnitOfWork : IDisposable, IAsyncDisposable
    {
        Task<IReadOnlyCollection<ISessionStateMemento>> GetSessionStates(object @event);
        Task<IReadOnlyCollection<ISessionStateMemento>> GetSessionStates(IEnumerable<object> events);
        Task<ISessionStateMemento?> GetSessionState(Guid sessionStateId);
        Task<ISessionStateMemento> Add(SessionStateEntity sessionState);
    }
}
