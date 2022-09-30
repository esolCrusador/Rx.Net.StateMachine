using Rx.Net.StateMachine.ObservableExtensions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Tests.Extensions
{
    public static class PersistanceExtensions
    {
        public static IObservable<int> PersistMessageId(this IObservable<int> messageObservable, StateMachineScope scope, string messagesCollectition = "Messages")
        {
            return messageObservable.TapAsync(messageId =>
                scope.AddOrUpdateItem(messagesCollectition, () => new List<int> { messageId }, items =>
                {
                    items.Add(messageId);
                    return items;
                })
            );
        }

        public static List<int> GetMessageIds(this StateMachineScope scope, string collectionName = "Messages")
        {
            return scope.GetItem<List<int>>(collectionName);
        }

        public static Task DeleteMessageIds(this StateMachineScope scope, string collectionName = "Messages")
        {
            return scope.DeleteItem(collectionName);
        }
    }
}
