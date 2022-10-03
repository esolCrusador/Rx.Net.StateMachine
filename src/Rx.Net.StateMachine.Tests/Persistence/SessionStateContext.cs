using Rx.Net.StateMachine.Persistance;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Rx.Net.StateMachine.Tests.Persistence
{
    public class TestSessionStateContext : ISessionStateContext<TestSessionStateEntity, UserContext>
    {
        public TestSessionStateEntity CreateNewSessionState(UserContext context)
        {
            return new TestSessionStateEntity
            {
                UserId = context.UserId,
                SessionId = Guid.NewGuid()
            };
        }

        public Expression<Func<TestSessionStateEntity, bool>> GetContextFilter(UserContext context)
        {
            return ss => ss.UserId == context.UserId;
        }
    }
}
