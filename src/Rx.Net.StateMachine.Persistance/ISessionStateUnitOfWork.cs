using Rx.Net.StateMachine.Persistance.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Persistance
{
    public interface ISessionStateUnitOfWork : IDisposable, IAsyncDisposable
    {
        Task<IReadOnlyCollection<SessionStateEntity>> GetSessionStates(object @event);
        Task<IReadOnlyCollection<SessionStateEntity>> GetSessionStates(IEnumerable<object> events);
        Task<SessionStateEntity?> GetSessionState(Guid sessionStateId);
        Task Add(SessionStateEntity sessionState);
        Task Save();
    }
}
