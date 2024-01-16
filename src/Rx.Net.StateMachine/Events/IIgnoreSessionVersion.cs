using System;

namespace Rx.Net.StateMachine.Events
{
    public interface IIgnoreSessionVersion
    {
        Guid SessionId { get; }
        int Version { get; }
    }
}
