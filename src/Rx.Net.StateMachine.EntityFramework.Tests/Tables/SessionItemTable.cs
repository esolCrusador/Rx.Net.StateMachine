using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.EntityFramework.Tests.Tables
{
    public class SessionItemTable
    {
        public string Id { get; set; }
        public Guid SessionStateId { get; set; }
        public string Value { get; set; }
    }
}
