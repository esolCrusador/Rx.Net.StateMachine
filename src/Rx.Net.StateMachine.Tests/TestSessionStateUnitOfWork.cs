using Rx.Net.StateMachine.Persistance;
using Rx.Net.StateMachine.Persistance.Entities;
using Rx.Net.StateMachine.Tests.Fakes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Tests
{
    public class SessionStateDataStore
    {
        public readonly List<SessionStateEntity> SessionStates = new List<SessionStateEntity>();
    }

    public class TestSessionStateUnitOfWorkFactory : ISessionStateUnitOfWorkFactory
    {
        private readonly SessionStateDataStore _dataStore;

        public TestSessionStateUnitOfWorkFactory(SessionStateDataStore dataStore)
        {
            _dataStore = dataStore;
        }

        public ISessionStateUnitOfWork Create()
        {
            return new TestSessionStateUnitOfWork(_dataStore);
        }
    }

    public class TestSessionStateUnitOfWork : ISessionStateUnitOfWork
    {
        private readonly SessionStateDataStore _dataStore;

        private List<SessionStateEntity> _added = new List<SessionStateEntity>();
        private HashSet<SessionStateEntity> _modified = new HashSet<SessionStateEntity>();

        public TestSessionStateUnitOfWork(SessionStateDataStore dataStore)
        {
            _dataStore = dataStore;
        }

        public Task<IReadOnlyCollection<SessionStateEntity>> GetSessionStates(object @event)
        {
            var sessionStates = _dataStore.SessionStates.AsQueryable().Where(GetFilter(@event))
                .AsEnumerable()
                .Select(s =>
                {
                    _modified.Add(s);
                    return s;
                })
                .ToList();

            return Task.FromResult<IReadOnlyCollection<SessionStateEntity>>(sessionStates);
        }
        private Expression<Func<SessionStateEntity, bool>> GetFilter(object @event)
        {
            if (@event is BotFrameworkMessage botFrameworkMessage)
                return ss => true; // TODO Replace with filter
            if (@event is BotFrameworkButtonClick botFrameworkButtonClick)
                return bf => true;

            throw new NotSupportedException($"Not supported event type {@event}");
        }
        public Task Add(SessionStateEntity entity)
        {
            _added.Add(entity);

            return Task.CompletedTask;
        }
        public Task Save()
        {
            if (_added.Count > 0)
            {
                _dataStore.SessionStates.AddRange(_added.Select(a => DeepClone(a)));
                _added.Clear();
            }

            if (_modified.Count > 0)
            {
                foreach (var updated in _modified)
                {
                    _dataStore.SessionStates[_dataStore.SessionStates.IndexOf(updated)] = DeepClone(updated);
                }

                _modified.Clear();
            }

            return Task.CompletedTask;
        }

        private static SessionStateEntity DeepClone(SessionStateEntity sessionState)
        {
            var result = JsonSerializer.Deserialize<SessionStateEntity>(JsonSerializer.Serialize(sessionState));
            result.Context = sessionState.Context;

            return result;
        }

        public void Dispose()
        {
            _modified.Clear();
            _added.Clear();
        }

        public ValueTask DisposeAsync()
        {
            _modified.Clear();
            _added.Clear();
            return default;
        }
    }
}
