using Rx.Net.StateMachine.EntityFramework.Awaiters;
using Rx.Net.StateMachine.EntityFramework.Tests.UnitOfWork;
using Rx.Net.StateMachine.Tests.Persistence;

namespace Rx.Net.StateMachine.Tests.DataAccess
{
    public class TestEFSessionStateUnitOfWork : EFSessionStateUnitOfWork<UserContext, int>
    {
    }
}
