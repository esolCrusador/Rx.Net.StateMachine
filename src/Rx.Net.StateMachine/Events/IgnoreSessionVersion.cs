using System;

namespace Rx.Net.StateMachine.Events
{
    public class IgnoreSessionVersion : IIgnoreSessionVersion
    {
        public required Guid SessionId { get; init; }
        public required int Version { get; init; }
    }
}
