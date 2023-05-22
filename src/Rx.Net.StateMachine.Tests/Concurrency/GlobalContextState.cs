using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Tests.Concurrency
{
    public class GlobalContextState
    {
        private List<Func<Task>>? _onBeforeNextSaveChanges;
        public void OnBeforeNextSaveChanges(Func<Task> onBeforeNextSaveChanges) =>
            (_onBeforeNextSaveChanges ??= new List<Func<Task>>()).Add(onBeforeNextSaveChanges);

        public async Task Execute()
        {
            var delegates = _onBeforeNextSaveChanges;
            if (delegates?.Count > 0)
            {
                var task = Task.WhenAll(delegates.Select(tf => tf()));
                delegates.Clear();
                await task;
            }
        }
    }
}
