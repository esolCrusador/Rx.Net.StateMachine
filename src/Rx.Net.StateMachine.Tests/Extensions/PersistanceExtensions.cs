using Rx.Net.StateMachine.Extensions;
using Rx.Net.StateMachine.Tests.Fakes;
using Rx.Net.StateMachine.Tests.Persistence;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Tests.Extensions
{
    public static class PersistanceExtensions
    {
        public static IObservable<TSource> DeleteMssages<TSource>(this IObservable<TSource> source, StateMachineScope scope, ChatFake bot, string collectionName = "Messages")
        {
            var userContext = scope.GetContext<UserContext>();
            return source.DisposeItems<TSource, int>(scope, messageIds => 
                Task.WhenAll(messageIds.Select(messageId => bot.DeleteBotMessage(userContext.BotId, userContext.ChatId, messageId)))
            );
        }
    }
}
