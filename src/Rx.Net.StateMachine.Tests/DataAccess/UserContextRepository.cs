using Microsoft.EntityFrameworkCore;
using Rx.Net.StateMachine.EntityFramework.ContextDfinition;
using Rx.Net.StateMachine.Tests.Persistence;
using System;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Tests.DataAccess
{
    public class UserContextRepository
    {
        private readonly SessionStateDbContextFactory<TestSessionStateDbContext, UserContext, int> _contextFactory;

        public UserContextRepository(SessionStateDbContextFactory<TestSessionStateDbContext, UserContext, int> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<UserContext> GetUserOrCreateContext(long botId, long chatId, string name, string username)
        {
            await using var context = _contextFactory.Create();
            var userContext = await context.Contexts.FirstOrDefaultAsync(ctx => ctx.BotId == botId && ctx.ChatId == chatId);
            if (userContext == null)
            {
                userContext = new UserContext
                {
                    BotId = botId,
                    ChatId = chatId,
                    Name = name,
                    Username = username,
                    User = new Entities.UserEntity
                    {
                        Name = name,
                    }
                };
                context.Contexts.Add(userContext);
                await context.SaveChangesAsync();
            }

            return userContext;
        }

        public async Task<UserContext> GetUserContext(long botId, long chatId)
        {
            await using var context = _contextFactory.Create();
            var userContext = await context.Contexts.FirstOrDefaultAsync(ctx => ctx.BotId == botId && ctx.ChatId == chatId)
                ?? throw new Exception("Context not found");

            return userContext;
        }
    }
}
