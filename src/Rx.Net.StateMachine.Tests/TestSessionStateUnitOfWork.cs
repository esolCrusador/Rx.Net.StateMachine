using Rx.Net.StateMachine.Persistance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Tests
{
    public class SessionStateDataStore<TSessionState>
    {
        public readonly List<TSessionState> SessionStates = new List<TSessionState>();
    }

    public class TestSessionStateUnitOfWorkFactory : ISessionStateUnitOfWorkFactory<TestSessionStateEntity>
    {
        private readonly SessionStateDataStore<TestSessionStateEntity> _dataStore;

        public TestSessionStateUnitOfWorkFactory(SessionStateDataStore<TestSessionStateEntity> dataStore)
        {
            _dataStore = dataStore;
        }

        public ISessionStateUnitOfWork<TestSessionStateEntity> Create()
        {
            return new TestSessionStateUnitOfWork(_dataStore);
        }
    }

    public class TestSessionStateUnitOfWork : ISessionStateUnitOfWork<TestSessionStateEntity>
    {
        private readonly SessionStateDataStore<TestSessionStateEntity> _dataStore;

        private List<TestSessionStateEntity> _added = new List<TestSessionStateEntity>();
        private HashSet<TestSessionStateEntity> _modified = new HashSet<TestSessionStateEntity>();

        public TestSessionStateUnitOfWork(SessionStateDataStore<TestSessionStateEntity> dataStore)
        {
            _dataStore = dataStore;
        }

        public Task<IReadOnlyCollection<TestSessionStateEntity>> GetSessionStates(Expression<Func<TestSessionStateEntity, bool>> filter)
        {
            var sessionStates = _dataStore.SessionStates.AsQueryable().Where(filter)
                .AsEnumerable()
                .Select(s =>
                {
                    _modified.Add(s);
                    return s;
                })
                .ToList();

            return Task.FromResult<IReadOnlyCollection<TestSessionStateEntity>>(sessionStates);
        }
        public Task Add(TestSessionStateEntity entity)
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

        private static TestSessionStateEntity DeepClone(TestSessionStateEntity sessionState) =>
            JsonSerializer.Deserialize<TestSessionStateEntity>(JsonSerializer.Serialize(sessionState));

        public void Dispose()
        {
            _modified.Clear();
            _added.Clear();
        }
    }
}
