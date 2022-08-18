using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static Rx.Net.StateMachine.Tests.WorkflowManager;

namespace Rx.Net.StateMachine.Tests
{
    public class SessionStateDataStore
    {
        public readonly List<SessionStateEntity> SessionStates = new List<SessionStateEntity>();
    }
    public class SessionStateUnitOfWork: IDisposable
    {
        private readonly SessionStateDataStore _dataStore;

        private List<SessionStateEntity> _added = new List<SessionStateEntity>();
        private HashSet<SessionStateEntity> _modified = new HashSet<SessionStateEntity>();

        public SessionStateUnitOfWork(SessionStateDataStore dataStore)
        {
            _dataStore = dataStore;
        }

        public IQueryable<SessionStateEntity> GetSessionStates(Expression<Func<SessionStateEntity, bool>> filter)
        {
            return _dataStore.SessionStates.AsQueryable().Where(filter)
                .AsEnumerable()
                .Select(s =>
                {
                    _modified.Add(s);
                    return s;
                })
                .AsQueryable();
        }
        public void Add(SessionStateEntity entity)
        {
            _added.Add(entity);
        }
        public Task Save()
        {
            if (_added.Count > 0)
            {
                _dataStore.SessionStates.AddRange(_added.Select(a => DeepClone(a)));
                _added.Clear();
            }

            if(_modified.Count > 0)
            {
                foreach(var updated in _modified)
                {
                    _dataStore.SessionStates[_dataStore.SessionStates.IndexOf(updated)] = DeepClone(updated);
                }

                _modified.Clear();
            }

            return Task.CompletedTask;
        }

        private static SessionStateEntity DeepClone(SessionStateEntity sessionState) =>
            JsonSerializer.Deserialize<SessionStateEntity>(JsonSerializer.Serialize(sessionState));

        public void Dispose()
        {
            _modified.Clear();
            _added.Clear();
        }
    }
}
