using System;

namespace Rx.Net.StateMachine.Events
{
    public interface IStaleSessionVersion
    {
        Guid SessionId { get; }
        int Version { get; }
    }
}
