using Rx.Net.StateMachine.EntityFramework.Tests.ContextDfinition;
using Rx.Net.StateMachine.EntityFramework.Tests.Tables;
using Rx.Net.StateMachine.Persistance;
using Rx.Net.StateMachine.Persistance.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.EntityFramework.Tests.UnitOfWork
{
    public class EFSessionStateUnitOfWork<TSessionStateEntity> : ISessionStateUnitOfWork<TSessionStateEntity>
        where TSessionStateEntity : SessionStateBaseEntity
    {
        record SessionStateData
        {
            public TSessionStateEntity SessionState { get; init; }
            public SessionStateTable Row { get; init; }
        }
        private readonly Dictionary<Guid, SessionStateData> _loadedSessionStates;
        private readonly SessionStateContext _sessionStateContext;

        public EFSessionStateUnitOfWork(SessionStateContext sessionStateContext)
        {
            _loadedSessionStates = new Dictionary<Guid, SessionStateData>();
            _sessionStateContext = sessionStateContext;
        }

        public Task Add(TSessionStateEntity sessionState)
        {
            var row = new SessionStateTable
            {
                SessionStateId = Guid.NewGuid(),
            };
            Map(sessionState, row);
            _loadedSessionStates.Add(row.SessionStateId, new SessionStateData
            {
                Row = row,
                SessionState = sessionState
            });
            _sessionStateContext.SessionStates.Add(row);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _sessionStateContext.Dispose();
        }

        public Task<IReadOnlyCollection<TSessionStateEntity>> GetSessionStates(Expression<Func<TSessionStateEntity, bool>> filter)
        {
            
        }

        public Task Save()
        {
            foreach(var pair in _loadedSessionStates.Values)
                Map(pair.Row, pair.SessionState);

            return _sessionStateContext.SaveChangesAsync();
        }

        private void Map(SessionStateTable row, TSessionStateEntity entity)
        {
            entity.WorkflowId = row.WorkflowId;
            entity.Counter = row.Counter;

            // Map steps, items, awaiters

            entity.Status = row.Status;
            entity.Result = row.Result;
        }

        private void Map(TSessionStateEntity entity, SessionStateTable row)
        {
            row.WorkflowId = entity.WorkflowId;
            row.Counter = entity.Counter;

            // Map steps, items, awaiters

            row.Status = entity.Status;
            row.Result = entity.Result;
        }
    }
}
