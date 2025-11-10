using Rx.Net.StateMachine.Flow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Extensions
{
    public static class DisposableItemsExtensions
    {
        public static IFlow<TItemId> PersistDisposableItem<TItemId>(this IFlow<TItemId> items, string messagesCollectition = "Messages")
        {
            return items.TapAsync((messageId, scope) => scope.PersistDisposableItem(messageId, messagesCollectition));
        }

        public static Task PersistDisposableItem<TItemId>(this StateMachineScope scope, TItemId messageId, string messagesCollectition = "Messages")
        {
            return scope.AddOrUpdateItemAsync(messagesCollectition, () => new List<TItemId> { messageId }, items =>
            {
                items.Add(messageId);
                return items;
            });
        }

        public static IFlow<TItem> PersistDisposableItem<TItem, TItemId>(this IFlow<TItem> messagesFlow, Func<TItem, TItemId> idSelector, string messagesCollectition = "Messages")
        {
            return messagesFlow.TapAsync((message, scope) =>
                scope.PersistDisposableItem(message, idSelector, messagesCollectition)
            );
        }

        public static Task PersistDisposableItem<TItem, TItemId>(this StateMachineScope scope, TItem message, Func<TItem, TItemId> idSelector, string messagesCollectition = "Messages")
        {
            return scope.AddOrUpdateItemAsync(messagesCollectition, () => new List<TItemId> { idSelector(message) }, items =>
            {
                items.Add(idSelector(message));
                return items;
            });
        }

        public static List<TItemId>? GetDisposableItems<TItemId>(this StateMachineScope scope, string collectionName = "Messages")
        {
            return scope.GetItem<List<TItemId>>(collectionName);
        }

        public static Task DeleteDisposableItems(this StateMachineScope scope, string collectionName = "Messages")
        {
            return scope.DeleteItemAsync(collectionName);
        }

        public static IFlow<TSource> DisposeItems<TSource, TItemId>(this IFlow<TSource> source, Func<IEnumerable<TItemId>, StateMachineScope, Task> disposeItems, string collectionName = "Messages")
        {
            return source.FinallyAsync(async (isExecuted, s, ex, scope) =>
            {
                if (!isExecuted)
                    return;

                var allMessages = source.Scope.GetItems<List<TItemId>>(collectionName);

                await disposeItems(allMessages.SelectMany(messages => messages), scope);
            });
        }

        public static IFlow<TSource> DisposeItems<TSource, TItemId>(this IFlow<TSource> source, Action<IEnumerable<TItemId>, StateMachineScope> disposeItems, string collectionName = "Messages")
        {
            return source.Finally((isExecuted, s, ex, scope) =>
            {
                if (!isExecuted)
                    return;

                var allMessages = source.Scope.GetItems<List<TItemId>>(collectionName);

                disposeItems(allMessages.SelectMany(messages => messages), scope);
            });
        }
    }
}
