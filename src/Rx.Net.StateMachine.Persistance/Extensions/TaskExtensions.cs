using System;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Persistance.Extensions
{
    public static class TaskExtensions
    {
        public static Task<ResultOrException<TResult>> ResultOrException<TResult>(this Task<TResult> task)
        {
            return task.ContinueWith(r => new Extensions.ResultOrException<TResult>
            {
                Result = r.Result,
                Exception = r.Exception
            });
        }
    }

    public struct ResultOrException<TResult>
    {
        public required Exception? Exception { get; init; }
        public required TResult? Result { get; set; }
    }
}
