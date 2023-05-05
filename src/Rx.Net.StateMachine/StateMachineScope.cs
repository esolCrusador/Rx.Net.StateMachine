﻿using Rx.Net.StateMachine.Exceptions;
using Rx.Net.StateMachine.Extensions;
using Rx.Net.StateMachine.Helpers;
using Rx.Net.StateMachine.States;
using Rx.Net.StateMachine.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine
{
    public struct StateMachineScope
    {
        public string StatePrefix { get; }
        public StateMachine StateMachine { get; }
        public SessionState SessionState { get; }
        public ISessionStateStorage SessionStateStorage { get; }
        public Guid SessionId => SessionState.SessionStateId.GetValue("SessionStateId");

        public StateMachineScope(StateMachine stateMachine, SessionState sessionState, ISessionStateStorage sessionStateRepository, string prefix = null)
        {
            StateMachine = stateMachine;
            SessionState = sessionState;
            SessionStateStorage = sessionStateRepository;
            StatePrefix = prefix;
        }

        public bool TryGetStep<TSource>(string stateId, out TSource stepValue) =>
            SessionState.TryGetStep(AddPrefix(stateId), StateMachine.SerializerOptions, out stepValue);

        public StateMachineScope BeginScope(string prefix) =>
            new StateMachineScope(StateMachine, SessionState, SessionStateStorage, AddPrefix(prefix));

        public async Task<StateMachineScope> BeginRecursiveScope(string prefix)
        {
            string depthName = GetDepthName(prefix);
            if (!SessionState.TryGetItem(depthName, StateMachine.SerializerOptions, out int depth))
            {
                depth = 1;
                SessionState.AddItem(depthName, depth, StateMachine.SerializerOptions);

                await SessionStateStorage.PersistItemState(SessionState);
            }

            return new StateMachineScope(StateMachine, SessionState, SessionStateStorage, AddPrefix(prefix));
        }

        public async Task<StateMachineScope> IncreaseRecursionDepth()
        {
            var depthName = GetDepthName(StatePrefix);
            int depth = GetRecoursionDepth() ?? throw new ItemNotFoundException(depthName);
            depth++;
            SessionState.UpdateItem(depthName, depth, StateMachine.SerializerOptions);

            await SessionStateStorage.PersistItemState(SessionState);

            return this;
        }

        public IEnumerable<TEvent> GetEvents<TEvent>(Func<TEvent, bool> matches) =>
            SessionState.GetEvents(matches, StateMachine.SerializerOptions);

        public Task AddStep<TState>(string stateId, TState stepState)
        {
            SessionState.AddStep(AddPrefix(stateId), stepState, StateMachine.SerializerOptions);

            return SessionStateStorage.PersistStepState(SessionState);
        }

        public Task AddItem<TItem>(string itemId, TItem item)
        {
            SessionState.AddItem(AddPrefix(itemId), item, StateMachine.SerializerOptions);

            return SessionStateStorage.PersistItemState(SessionState);
        }

        public Task AddOrUpdateItem<TItem>(string itemId, Func<TItem> getItemToAdd, Func<TItem, TItem> updateItem)
        {
            SessionState.AddOrUpdateItem(AddPrefix(itemId), getItemToAdd, updateItem, StateMachine.SerializerOptions);

            return SessionStateStorage.PersistItemState(SessionState);
        }

        public Task UpdateItem<TItem>(string itemId, TItem item)
        {
            SessionState.UpdateItem(AddPrefix(itemId), item, StateMachine.SerializerOptions);

            return SessionStateStorage.PersistItemState(SessionState);
        }

        public TItem GetItem<TItem>(string itemId)
        {
            return SessionState.GetItem<TItem>(AddPrefix(itemId), StateMachine.SerializerOptions);
        }

        /// <summary>
        /// Gets multiple items if scope is recoursive if not gets single item
        /// </summary>
        public IEnumerable<TItem> GetItems<TItem>(string itemId)
        {
            var recoursionDepth = GetRecoursionDepth();
            if (recoursionDepth == null)
                yield return GetItem<TItem>(itemId);

            while (recoursionDepth > 0)
                yield return SessionState.GetItem<TItem>(AddPrefix(StatePrefix, recoursionDepth--, itemId), StateMachine.SerializerOptions);
        }

        public Task DeleteItem(string itemId)
        {
            SessionState.DeleteItem(itemId);

            return SessionStateStorage.PersistItemState(SessionState);
        }

        public Task AddEvent<TEvent>(TEvent @event)
        {
            SessionState.AddEvent<TEvent>(@event);

            return SessionStateStorage.PersistEventState(SessionState);
        }

        public Task EventHandled<TEvent>(TEvent e)
        {
            SessionState.MarkEventAsHandled(e, StateMachine.SerializerOptions);

            return SessionStateStorage.PersistEventState(SessionState);
        }

        public Task AddEventAwaiter<TEvent>(string stateId)
        {
            SessionState.AddEventAwaiter<TEvent>(AddPrefix(stateId));

            return SessionStateStorage.PersistEventAwaiter(SessionState);
        }

        public Task MakeDefault(bool isDefault)
        {
            SessionState.MakeDefault(isDefault);

            return SessionStateStorage.PersistIsDefault(SessionState);
        }

        public Task RemoveScopeAwaiters()
        {
            SessionState.RemoveEventAwaiters(StatePrefix);

            return SessionStateStorage.PersistEventAwaiter(SessionState);
        }

        public TContext GetContext<TContext>() => (TContext)SessionState.Context;

        public string GetStateString()
        {
            using var stateStream = new MemoryStream();
            var serializerOptions = new JsonSerializerOptions(StateMachine.SerializerOptions)
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull | JsonIgnoreCondition.WhenWritingDefault
            };

            JsonSerializer.Serialize(stateStream, SessionState.ToMinimalState(), StateMachine.SerializerOptions);

            var zipped = CompressionHelper.Zip(stateStream);
            Console.WriteLine("Initial Length: {0}, Zipped Length: {1}", stateStream.Length, zipped.Length);

            return zipped;
        }

        private static string GetDepthName(string prefix) => $"{prefix}[depth]";

        private int? GetRecoursionDepth()
        {
            string depthName = GetDepthName(StatePrefix);
            if (!SessionState.TryGetItem<int>(depthName, StateMachine.SerializerOptions, out var depth))
                return null;

            return depth;
        }

        private string AddPrefix(string stateId) =>
            AddPrefix(StatePrefix, GetRecoursionDepth(), stateId);

        private static string AddPrefix(string prefix, int? recoursionDepth, string stateId)
        {
            if (prefix == null)
                return stateId;

            if (recoursionDepth == null)
                return $"{prefix}.{stateId}";

            return $"{prefix}-{recoursionDepth}.{stateId}";
        }
    }
}
