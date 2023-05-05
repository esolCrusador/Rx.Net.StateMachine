﻿using Rx.Net.StateMachine.EntityFramework.Tables;
using Rx.Net.StateMachine.EntityFramework.Tests.UnitOfWork;
using Rx.Net.StateMachine.Tests.Events;
using Rx.Net.StateMachine.Tests.Fakes;
using Rx.Net.StateMachine.Tests.Persistence;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Linq;

namespace Rx.Net.StateMachine.Tests.DataAccess
{
    public class TestEFSessionStateUnitOfWork : EFSessionStateUnitOfWork<UserContext, int>
    {
        protected override Expression<Func<SessionStateTable<UserContext, int>, bool>> GetFilter(object @event)
        {
            if (@event is BotFrameworkMessage botFrameworkMessage)
                return ss => ss.Context.BotId == botFrameworkMessage.BotId && ss.Context.ChatId == botFrameworkMessage.ChatId && ss.IsDefault > 0;
            if (@event is BotFrameworkButtonClick botFrameworkButtonClick)
            {
                if(WorkflowCallbackQuery.TryParse(botFrameworkButtonClick.SelectedValue, out var query))
                {
                    if(query.SessionId != null)
                        return ss => ss.SessionStateId == query.SessionId.Value;
                }

                return ss => ss.Awaiters.Any(aw => aw.Context.BotId == botFrameworkButtonClick.BotId
                    && aw.Context.ChatId == botFrameworkButtonClick.ChatId
                    && aw.TypeName == typeof(BotFrameworkButtonClick).AssemblyQualifiedName
                );
            }
            if (@event is TaskCreatedEvent taskCreatedEvent)
                return ss => ss.Awaiters.Any(aw => aw.TypeName == typeof(TaskCreatedEvent).AssemblyQualifiedName);
            if (@event is TaskStateChanged taskStateChangedEvent)
                return ss => ss.Awaiters.Any(aw => aw.TypeName == typeof(TaskStateChanged).AssemblyQualifiedName);
            if (@event is TimeoutEvent timeoutEvent)
                return ss => ss.SessionStateId == timeoutEvent.SessionId;

            throw new NotSupportedException($"Not supported event type {@event.GetType().FullName}");
        }
    }
}