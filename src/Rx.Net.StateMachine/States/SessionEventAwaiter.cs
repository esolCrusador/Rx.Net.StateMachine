using Rx.Net.StateMachine.Extensions;
using System;

namespace Rx.Net.StateMachine.States
{
    public class SessionEventAwaiter
    {
        public Guid AwaiterId { get; }
        public string Name { get; }
        public int SequenceNumber { get; }
        private string? _typeName;
        private Type? _type;
        public string TypeName { get => _typeName ??= GetTypeName(_type.GetValue("_type")); set => _typeName = value; }
        public Type Type { get => _type ??= Type.GetType(_typeName); set => _type = value; }

        public SessionEventAwaiter(Type type, string name, int sequenceNumber)
        {
            AwaiterId = Guid.NewGuid();
            Name = name;
            Type = type;
            SequenceNumber = sequenceNumber;
        }

        public SessionEventAwaiter(Guid awaiterId, string name, string typeName, int sequenceNumber)
        {
            AwaiterId = awaiterId;
            Name = name;
            TypeName = typeName;
            SequenceNumber = sequenceNumber;
        }

        public static string GetTypeName(Type type) => type.AssemblyQualifiedName;
    }
}
