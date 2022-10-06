using Rx.Net.StateMachine.Persistance.Entities;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Persistance
{
    public interface ISessionStateUnitOfWork<TSessionState> : IDisposable
        where TSessionState : SessionStateBaseEntity
    {
        Task<IReadOnlyCollection<TSessionState>> GetSessionStates(Expression<Func<TSessionState, bool>> filter);
        Task Add(TSessionState sessionState);
        Task Save();
    }
}
