using System;
using System.Linq.Expressions;

namespace Rx.Net.StateMachine.Persistance
{
    public interface ISessionStateContextConnector<TSessionState, TContext>
    {
        Expression<Func<TSessionState, bool>> GetContextFilter(TContext context);
        TSessionState CreateNewSessionState(TContext context);
    }
}
