using Rx.Net.StateMachine.ObservableExtensions;
using Rx.Net.StateMachine.Tests.Fakes;
using Rx.Net.StateMachine.Tests.Persistence;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
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

        public static IObservable<BotFrameworkMessage> PersistMessageId(this IObservable<BotFrameworkMessage> messageObservable, StateMachineScope scope, string messagesCollectition = "Messages")
        {
            return messageObservable.TapAsync(message =>
                scope.AddOrUpdateItem(messagesCollectition, () => new List<int> { message.MessageId }, items =>
                {
                    items.Add(message.MessageId);
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

        public static IObservable<TSource> DeleteMssages<TSource>(this IObservable<TSource> source, StateMachineScope scope, BotFake botFake, string collectionName = "Messages")
        {
            return source.FinallyAsync(async (isExecuted, source, ex) =>
            {
                if (!isExecuted)
                    return;

                var allMessages = scope.GetItems<List<int>>(collectionName);

                List<Task> deleteTasks = new List<Task>();

                foreach (var messages in allMessages)
                    foreach (var message in messages)
                        deleteTasks.Add(
                            botFake.DeleteBotMessage(scope.GetContext<UserContext>().UserId, message)
                        );
                await Task.WhenAll(deleteTasks);
            });
        }
    }
}
