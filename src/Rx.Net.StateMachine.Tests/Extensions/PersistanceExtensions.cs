using Rx.Net.StateMachine.Extensions;
using Rx.Net.StateMachine.Flow;
using Rx.Net.StateMachine.Tests.Fakes;
using Rx.Net.StateMachine.Tests.Persistence;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Tests.Extensions
{
    public static class PersistanceExtensions
    {
        public static IFlow<TSource> DeleteMssages<TSource>(this IFlow<TSource> source, ChatFake bot, string collectionName = "Messages")
        {
            var userContext = source.Scope.GetContext<UserContext>();
            return source.DisposeItems<TSource, int>(messageIds => 
                Task.WhenAll(messageIds.Select(messageId => bot.DeleteBotMessage(userContext.BotId, userContext.ChatId, messageId)))
            );
        }
    }
}
