using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Rx.Net.StateMachine.Persistance
{
    public interface ISessionStateContext<TSessionState, TContext>
    {
        Expression<Func<TSessionState, bool>> GetContextFilter(TContext context);
        TSessionState CreateNewSessionState(TContext context);
    }
}
