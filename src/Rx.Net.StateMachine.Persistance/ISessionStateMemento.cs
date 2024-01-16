using Rx.Net.StateMachine.Persistance.Entities;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Persistance
{
    public interface ISessionStateMemento
    {
        SessionStateEntity Entity { get; }
        Task Save();
    }
}
