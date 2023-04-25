using Microsoft.EntityFrameworkCore;
using Rx.Net.StateMachine.EntityFramework.ContextDfinition;
using Rx.Net.StateMachine.Tests.Persistence;
using System;

namespace Rx.Net.StateMachine.Tests
{
    public class TestSessionStateDbContext : SessionStateDbContext<UserContext, Guid>
    {
        public TestSessionStateDbContext(DbContextOptions options) : base(options)
        {
        }
    }
}
