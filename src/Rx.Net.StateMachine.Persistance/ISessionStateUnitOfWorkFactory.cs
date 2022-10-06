using Rx.Net.StateMachine.Persistance.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Rx.Net.StateMachine.Persistance
{
    public interface ISessionStateUnitOfWorkFactory<TSessionState>
        where TSessionState : SessionStateBaseEntity
    {
        ISessionStateUnitOfWork<TSessionState> Create();
    }
}
