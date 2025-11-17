using System;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Persistance.Extensions
{
    public static class TaskExtensions
    {
        public static async Task<ResultOrException<TResult>> ResultOrException<TResult>(this Task<TResult> task)
        {
            try
            {
                return new ResultOrException<TResult>
                {
                    Exception = null,
                    Result = await task
                };
            }
            catch (Exception ex)
            {
                return new ResultOrException<TResult>
                {
                    Exception = ex,
                    Result = default
                };
            }
        }
    }

    public struct ResultOrException<TResult>
    {
        public required Exception? Exception { get; init; }
        public required TResult? Result { get; set; }
    }
}
