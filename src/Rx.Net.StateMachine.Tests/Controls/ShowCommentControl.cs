using Rx.Net.StateMachine.Tests.DataAccess;
using Rx.Net.StateMachine.Tests.Fakes;
using Rx.Net.StateMachine.Tests.Models;
using Rx.Net.StateMachine.Tests.Persistence;
using System;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Tests.Controls
{
    public class ShowCommentControl: IControl
    {
        private readonly UserContextRepository _userContextRepository;
        private readonly ChatFake _chat;

        public ShowCommentControl(UserContextRepository userContextRepository, ChatFake chat)
        {
            _userContextRepository = userContextRepository;
            this._chat = chat;
        }
        public async Task<int> ShowComment(UserContext userContext, CommentModel comment, int taskMessageId)
        {
            var user = await _userContextRepository.GetUserContext(comment.UserId)
                ?? throw new Exception("User not found");
            return await _chat.SendBotMessage(
                userContext.BotId,
                userContext.ChatId,
                $"({user.Name})[tg://user/{user.ChatId}]:\r\n{comment.Text}",
                taskMessageId
            );
        }
    }
}
