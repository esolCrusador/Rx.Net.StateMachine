using Microsoft.EntityFrameworkCore;
using Rx.Net.StateMachine.EntityFramework.ContextDfinition;
using Rx.Net.StateMachine.Tests.Entities;
using Rx.Net.StateMachine.Tests.Events;
using Rx.Net.StateMachine.Tests.Models;
using Rx.Net.StateMachine.Tests.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine.Tests.DataAccess
{
    public class TaskRepository
    {
        private readonly SessionStateDbContextFactory<TestSessionStateDbContext, UserContext, int> _contextFactory;
        private readonly MessageQueue _messageQueue;
        private static readonly Expression<Func<TaskEntity, TaskModel>> TaskSelectorExpression = t => new TaskModel
        {
            TaskId = t.TaskId,
            Title = t.Title,
            State = t.State,
            AssigneeId = t.AssigneeId,
            SupervisorId = t.SupervisorId,
            Description = t.Description,
            Comments = t.Comments.Select(c => new CommentModel
            {
                CommentId = c.CommentId,
                Text = c.Text,
                UserId = c.UserId
            }).ToList()
        };
        private static readonly Func<TaskEntity, TaskModel> TaskSelector = TaskSelectorExpression.Compile();

        public TaskRepository(SessionStateDbContextFactory<TestSessionStateDbContext, UserContext, int> contextFactory, MessageQueue messageQueue)
        {
            _contextFactory = contextFactory;
            _messageQueue = messageQueue;
        }

        public async Task<TaskModel> GetTask(int taskId)
        {
            await using var context = _contextFactory.Create();

            return await context.Tasks.Where(t => t.TaskId == taskId)
                .Select(t => new TaskModel
                {
                    TaskId = t.TaskId,
                    Title = t.Title,
                    State = t.State,
                    AssigneeId = t.AssigneeId,
                    SupervisorId = t.SupervisorId,
                    Description = t.Description,
                    Comments = t.Comments.Select(c => new CommentModel
                    {
                        CommentId = c.CommentId,
                        Text = c.Text,
                        UserId = c.UserId
                    }).ToList()
                }).FirstAsync();
        }

        public async Task<int> CreateTask(string title, string description, Guid assigneeId)
        {
            await using var context = _contextFactory.Create();
            var task = new TaskEntity
            {
                Title = title,
                Description = description,
                AssigneeId = assigneeId,
                State = TaskState.ToDo
            };
            context.Tasks.Add(task);

            await context.SaveChangesAsync();
            await _messageQueue.Send(new TaskCreatedEvent { TaskId = task.TaskId, UserId = assigneeId });

            return task.TaskId;
        }

        public async Task<TaskModel> UpdateTaskState(int taskId, TaskState state, Dictionary<string, string> eventContext)
        {
            await using var context = _contextFactory.Create();
            var taskEntity = await context.Tasks.Include(t => t.Comments).FirstOrDefaultAsync(t => t.TaskId == taskId);
            if (taskEntity == null)
                throw new Exception("Not Found");

            taskEntity.State = state;
            await context.SaveChangesAsync();
            await _messageQueue.Send(new TaskStateChanged { TaskId = taskId, State = state, Context = eventContext });

            return TaskSelector(taskEntity);
        }

        public async Task<CommentModel> AddComment(int taskId, Guid userId, string comment, Dictionary<string, string> eventContext)
        {
            await using var context = _contextFactory.Create();
            var commentEntity = new TaskCommentEntity
            {
                TaskId = taskId,
                UserId = userId,
                Text = comment
            };
            context.TaskComments.Add(commentEntity);
            await context.SaveChangesAsync();

            await _messageQueue.Send(new TaskCommentAdded
            {
                TaskId = taskId,
                CommentId = commentEntity.CommentId,
                Text = comment,
                Context = eventContext,
                UserId = userId
            });

            return new CommentModel { CommentId = commentEntity.CommentId, Text = commentEntity.Text, UserId = commentEntity.UserId };
        }
    }
}
