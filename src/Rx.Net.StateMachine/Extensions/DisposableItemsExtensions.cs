using Rx.Net.StateMachine.ObservableExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Extensions
{
    public static class DisposableItemsExtensions
    {
        public static IObservable<TItemId> PersistDisposableItem<TItemId>(this IObservable<TItemId> items, StateMachineScope scope, string messagesCollectition = "Messages")
        {
            return items
                .TapAsync(messageId => scope.PersistDisposableItem(messageId, messagesCollectition));
        }

        public static Task PersistDisposableItem<TItemId>(this StateMachineScope scope, TItemId messageId, string messagesCollectition = "Messages")
        {
            return scope.AddOrUpdateItem(messagesCollectition, () => new List<TItemId> { messageId }, items =>
            {
                items.Add(messageId);
                return items;
            });
        }

        public static IObservable<TItem> PersistDisposableItem<TItem, TItemId>(this IObservable<TItem> messageObservable, StateMachineScope scope, Func<TItem, TItemId> idSelector, string messagesCollectition = "Messages")
        {
            return messageObservable.TapAsync(message =>
                scope.PersistDisposableItem(message, idSelector, messagesCollectition)
            );
        }

        public static Task PersistDisposableItem<TItem, TItemId>(this StateMachineScope scope, TItem message, Func<TItem, TItemId> idSelector, string messagesCollectition = "Messages")
        {
            return scope.AddOrUpdateItem(messagesCollectition, () => new List<TItemId> { idSelector(message) }, items =>
            {
                items.Add(idSelector(message));
                return items;
            });
        }

        public static List<TItemId> GetDisposableItems<TItemId>(this StateMachineScope scope, string collectionName = "Messages")
        {
            return scope.GetItem<List<TItemId>>(collectionName);
        }

        public static Task DeleteDisposableItems(this StateMachineScope scope, string collectionName = "Messages")
        {
            return scope.DeleteItem(collectionName);
        }

        public static IObservable<TSource> DisposeItems<TSource, TItemId>(this IObservable<TSource> source, StateMachineScope scope, Func<IEnumerable<TItemId>, Task> disposeItems, string collectionName = "Messages")
        {
            return source.FinallyAsync(async (isExecuted, source, ex) =>
            {
                if (!isExecuted)
                    return;

                var allMessages = scope.GetItems<List<TItemId>>(collectionName);

                await disposeItems(allMessages.SelectMany(messages => messages));
            });
        }
    }
}
