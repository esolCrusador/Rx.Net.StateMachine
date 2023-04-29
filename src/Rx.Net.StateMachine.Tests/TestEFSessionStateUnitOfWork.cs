using Rx.Net.StateMachine.EntityFramework.Tables;
using Rx.Net.StateMachine.EntityFramework.Tests.UnitOfWork;
using Rx.Net.StateMachine.Tests.Fakes;
using Rx.Net.StateMachine.Tests.Persistence;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Linq;

namespace Rx.Net.StateMachine.Tests
{
    public class TestEFSessionStateUnitOfWork : EFSessionStateUnitOfWork<UserContext, int>
    {
        protected override Expression<Func<SessionStateTable<UserContext, int>, bool>> GetFilter(object @event)
        {
            if (@event is BotFrameworkMessage botFrameworkMessage)
                return ss => ss.Context.UserId == botFrameworkMessage.UserId;
            if(@event is BotFrameworkButtonClick botFrameworkButtonClick)
                return ss => ss.Awaiters.Any(aw => aw.Context.UserId == botFrameworkButtonClick.UserId && aw.TypeName == typeof(BotFrameworkButtonClick).AssemblyQualifiedName);

            throw new NotSupportedException($"Not supported event type {@event.GetType().FullName}");
        }
    }
}
