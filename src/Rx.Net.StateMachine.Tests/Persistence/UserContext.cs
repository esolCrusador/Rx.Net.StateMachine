using System;
using System.ComponentModel.DataAnnotations;

namespace Rx.Net.StateMachine.Tests.Persistence
{
    public class UserContext
    {
        [Key] public Guid UserId { get; set; }
    }
}
