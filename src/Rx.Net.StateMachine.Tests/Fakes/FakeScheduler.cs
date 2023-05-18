using Rx.Net.StateMachine.Tests.DataAccess;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Tests.Fakes
{
    public class FakeScheduler
    {
        private DateTimeOffset? _utcNow;
        public DateTimeOffset UtcNow { get => _utcNow ?? DateTimeOffset.UtcNow; set => _utcNow = value; }
        private readonly MessageQueue _messageQueue;
        private readonly List<ScheduledEvent> _tasks = new();

        public FakeScheduler(MessageQueue messageQueue)
        {
            this._messageQueue = messageQueue;
        }

        public Task ScheduleEvent(object ev, TimeSpan shift)
        {
            _tasks.Add(new ScheduledEvent(UtcNow + shift, ev));
            return Task.CompletedTask;
        }
        
        public async Task RewindTime(TimeSpan timeSpan)
        {
            UtcNow += timeSpan;

            List<ScheduledEvent>? readyToGo = null;
            foreach (var task in _tasks)
                if (task.Time < UtcNow)
                    (readyToGo ??= new List<ScheduledEvent>()).Add(task);

            if(readyToGo != null)
                foreach (var task in readyToGo)
                {
                    await _messageQueue.SendAndWait(task.Event);
                    _tasks.Remove(task);
                }
        }

        class ScheduledEvent
        {
            public DateTimeOffset Time { get; }
            public object Event { get; }

            public ScheduledEvent(DateTimeOffset time, object @event)
            {
                Time = time;
                Event = @event;
            }
        }
    }
}
