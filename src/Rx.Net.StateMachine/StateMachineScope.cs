using Rx.Net.StateMachine.Events;
using Rx.Net.StateMachine.Exceptions;
using Rx.Net.StateMachine.States;
using Rx.Net.StateMachine.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Rx.Net.StateMachine
{
    public struct StateMachineScope
    {
        public string? StatePrefix { get; }
        public StateMachine StateMachine { get; }
        public SessionState SessionState { get; }
        public ISessionStateStorage SessionStateStorage { get; }
        public CancellationToken CancellationToken { get; }
        public readonly Guid SessionId => SessionState.SessionStateId ?? throw new ArgumentException($"{nameof(SessionState)}.{nameof(SessionState.SessionStateId)} was not initialized");
        public readonly int Version => SessionState.Version;

        public StateMachineScope(StateMachine stateMachine, SessionState sessionState, ISessionStateStorage sessionStateRepository, CancellationToken cancellationToken, string? prefix = null)
        {
            StateMachine = stateMachine;
            SessionState = sessionState;
            SessionStateStorage = sessionStateRepository;
            CancellationToken = cancellationToken;
            StatePrefix = prefix;
        }

        public bool TryGetStep<TSource>(string stateId, [MaybeNullWhen(false)] out TSource? stepValue) =>
            SessionState.TryGetStep(AddPrefix(stateId), StateMachine.SerializerOptions, null, out stepValue);

        public bool TryGetStep<TSource>(string stateId, Func<JsonElement, JsonSerializerOptions, TSource>? deserializeOldValue, [MaybeNullWhen(false)] out TSource? stepValue) =>
            SessionState.TryGetStep(AddPrefix(stateId), StateMachine.SerializerOptions, deserializeOldValue, out stepValue);

        public StateMachineScope BeginScope(string prefix) =>
            new StateMachineScope(StateMachine, SessionState, SessionStateStorage, CancellationToken, AddPrefix(prefix));

        public StateMachineScope EndScope(string prefix) =>
            new StateMachineScope(StateMachine, SessionState, SessionStateStorage, CancellationToken, RemovePrefix(prefix));

        public async Task<StateMachineScope> BeginRecursiveScopeAsync(string prefix)
        {
            string depthName = GetDepthName(AddPrefix(prefix));
            if (!SessionState.TryGetItem(depthName, StateMachine.SerializerOptions, out int depth))
            {
                depth = 1;
                SessionState.AddItem(depthName, depth, StateMachine.SerializerOptions);

                await SessionStateStorage.PersistItemState(SessionState);
            }

            return new StateMachineScope(StateMachine, SessionState, SessionStateStorage, CancellationToken, AddPrefix(prefix));
        }

        public async Task<StateMachineScope> IncreaseRecursionDepthAsync()
        {
            var depthName = GetDepthName(StatePrefix);
            int depth = GetRecoursionDepth() ?? throw new ItemNotFoundException(depthName);
            depth++;
            SessionState.UpdateItem(depthName, depth, StateMachine.SerializerOptions);

            await SessionStateStorage.PersistItemState(SessionState);

            return this;
        }

        public IEnumerable<TEvent> GetEvents<TEvent>(IEventAwaiter<TEvent> eventAwaiter, Func<TEvent, bool>? matches) =>
            SessionState.GetEvents(eventAwaiter, matches);

        public Task AddStepAsync<TState>(string stateId, TState stepState)
        {
            SessionState.AddStep(AddPrefix(stateId), stepState, StateMachine.SerializerOptions);

            return SessionStateStorage.PersistStepState(SessionState);
        }

        public Task AddItemAsync<TItem>(string itemId, TItem item)
        {
            SessionState.AddItem(AddPrefix(itemId), item, StateMachine.SerializerOptions);

            return SessionStateStorage.PersistItemState(SessionState);
        }

        public void AddOrUpdateItem<TItem>(string itemId, Func<TItem> getItemToAdd, Func<TItem, TItem> updateItem)
        {
            SessionState.AddOrUpdateItem(AddPrefix(itemId), getItemToAdd, updateItem, StateMachine.SerializerOptions);
        }

        public Task AddOrUpdateItemAsync<TItem>(string itemId, Func<TItem> getItemToAdd, Func<TItem, TItem> updateItem)
        {
            SessionState.AddOrUpdateItem(AddPrefix(itemId), getItemToAdd, updateItem, StateMachine.SerializerOptions);

            return SessionStateStorage.PersistItemState(SessionState);
        }

        public void AddOrUpdateItem<TItem>(string itemId, TItem item)
        {
            SessionState.AddOrUpdateItem(AddPrefix(itemId), item);
        }

        public Task AddOrUpdateItemAsync<TItem>(string itemId, TItem item)
        {
            SessionState.AddOrUpdateItem(AddPrefix(itemId), item);

            return SessionStateStorage.PersistItemState(SessionState);
        }

        public Task AddOrUpdateGlobalItemAsync<TItem>(string itemId, Func<TItem> getItemToAdd, Func<TItem, TItem> updateItem)
        {
            SessionState.AddOrUpdateItem($"Global.{itemId}", getItemToAdd, updateItem, StateMachine.SerializerOptions);

            return SessionStateStorage.PersistItemState(SessionState);
        }

        public Task UpdateGlobalItemAsync<TItem>(string itemId, Func<TItem?, TItem> updateItem)
        {
            SessionState.UpdateItem($"Global.{itemId}", updateItem, StateMachine.SerializerOptions);

            return SessionStateStorage.PersistItemState(SessionState);
        }

        public Task AddOrUpdateGlobalItemAsync<TItem>(string itemId, Func<TItem> getItemToAdd, Action<TItem> updateItem)
        {
            SessionState.AddOrUpdateItem($"Global.{itemId}", getItemToAdd, updateItem, StateMachine.SerializerOptions);

            return SessionStateStorage.PersistItemState(SessionState);
        }

        public Task AddGlobalItemAsync<TItem>(string itemId, TItem item)
        {
            SessionState.AddItem($"Global.{itemId}", item, StateMachine.SerializerOptions);

            return SessionStateStorage.PersistItemState(SessionState);
        }

        public Task AddOrUpdateGlobalItemAsync<TItem>(string itemId, TItem item)
        {
            SessionState.UpdateItem($"Global.{itemId}", item, StateMachine.SerializerOptions);

            return SessionStateStorage.PersistItemState(SessionState);
        }

        public IItems GetGlobalItemsAndVariables() => SessionState.GetItemsByPrefix("Global.").ToDictionary();
        public Task SetVariable(string name, string? value)
        {
            SessionState.UpdateItem(GetVariableItemName(name), value, StateMachine.SerializerOptions);

            return SessionStateStorage.PersistItemState(SessionState);
        }
        public Dictionary<string, string?> GetVariables(IReadOnlyCollection<string> names)
        {
            Dictionary<string, string?> result = new Dictionary<string, string?>(names.Count);
            foreach (var name in names)
            {
                if (SessionState.TryGetItem<string?>(GetVariableItemName(name), StateMachine.SerializerOptions, out var item))
                    result.Add(name, item);
                else
                    result.Add(name, null);
            }

            return result;
        }

        public static string GetVariableItemName(string variableName) => $"Global.Variable.{variableName}";
        public string? GetVariableOrDefault(string variableName, string? defaultValue = default) =>
            SessionState.GetItemOrDefault<string?>(GetVariableItemName(variableName), StateMachine.SerializerOptions, defaultValue);

        public bool TryGetVariable(string variableName, [MaybeNullWhen(false)] out string? result) =>
            SessionState.TryGetItem<string?>(GetVariableItemName(variableName), StateMachine.SerializerOptions, out result);

        public bool TryGetGlobalItem<TItem>(string itemId, [MaybeNullWhen(false)] out TItem? item)
        {
            return SessionState.TryGetItem($"Global.{itemId}", StateMachine.SerializerOptions, out item);
        }

        public Task UpdateItemAsync<TItem>(string itemId, TItem item)
        {
            UpdateItem(itemId, item);

            return SessionStateStorage.PersistItemState(SessionState);
        }

        public void UpdateItem<TItem>(string itemId, TItem item)
        {
            SessionState.UpdateItem(AddPrefix(itemId), item, StateMachine.SerializerOptions);
        }

        public TItem GetItem<TItem>(string itemId)
        {
            return SessionState.GetItem<TItem>(AddPrefix(itemId), StateMachine.SerializerOptions);
        }

        public bool TryGetItem<TItem>(string itemId, [MaybeNullWhen(false)] out TItem item)
        {
            return SessionState.TryGetItem<TItem>(AddPrefix(itemId), StateMachine.SerializerOptions, out item);
        }

        public TItem? GetItemOrDefault<TItem>(string itemId, TItem? defaultItem = default)
        {
            return SessionState.GetItemOrDefault<TItem>(AddPrefix(itemId), StateMachine.SerializerOptions, defaultItem);
        }

        public TItem? GetGlobalItemOrDefault<TItem>(string itemId, TItem? defaultItem = default)
        {
            return SessionState.GetItemOrDefault<TItem>($"Global.{itemId}", StateMachine.SerializerOptions, defaultItem);
        }

        /// <summary>
        /// Gets multiple items if scope is recoursive if not gets single item
        /// </summary>
        public IEnumerable<TItem> GetItems<TItem>(string itemId)
        {
            var recoursionDepth = GetRecoursionDepth();
            if (recoursionDepth == null)
                yield return GetItem<TItem>(itemId)
                    ?? throw new InvalidOperationException($"Could not get item {itemId}");

            while (recoursionDepth > 0)
                yield return SessionState.GetItem<TItem>(AddPrefix(StatePrefix, recoursionDepth--, itemId), StateMachine.SerializerOptions)
                    ?? throw new InvalidOperationException($"Could not get items {itemId} for recoursing depth {recoursionDepth + 1}");
        }

        public Task DeleteItemAsync(string itemId)
        {
            SessionState.DeleteItem(itemId);

            return SessionStateStorage.PersistItemState(SessionState);
        }

        public Task AddEventAsync(object @event, IEnumerable<IEventAwaiter> eventAwaiters)
        {
            SessionState.AddEvent(@event, eventAwaiters);

            return SessionStateStorage.PersistEventState(SessionState);
        }

        public Task EventHandledAsync<TEvent>(TEvent e)
            where TEvent : class
        {
            SessionState.MarkEventAsHandled(e, StateMachine.SerializerOptions);

            return SessionStateStorage.PersistEventState(SessionState);
        }

        public Task AddEventAwaiterAsync<TEvent>(string stateId, IEventAwaiter<TEvent> eventAwaiter)
        {
            SessionState.AddEventAwaiter<TEvent>(AddPrefix(stateId), eventAwaiter);

            return SessionStateStorage.PersistEventAwaiter(SessionState);
        }

        public Task MakeDefaultAsync(bool isDefault)
        {
            SessionState.MakeDefault(isDefault);

            return SessionStateStorage.PersistIsDefault(SessionState);
        }

        public Task RemoveScopeAwaitersAsync()
        {
            SessionState.RemoveEventAwaiters(StatePrefix ?? throw new InvalidOperationException("StatePrefix is null"));

            return SessionStateStorage.PersistEventAwaiter(SessionState);
        }

        public TContext GetContext<TContext>() => (TContext)SessionState.Context;

        public string GetStateString() => SessionState.ToMinimalState().GetStateString(StateMachine.SerializerOptions);

        private static string GetDepthName(string? prefix) => $"{prefix}[depth]";

        public int? GetRecoursionDepth()
        {
            string depthName = GetDepthName(StatePrefix);
            if (!SessionState.TryGetItem<int>(depthName, StateMachine.SerializerOptions, out var depth))
                return null;

            return depth;
        }

        private string AddPrefix(string stateId) =>
            AddPrefix(StatePrefix, GetRecoursionDepth(), stateId);

        private string? RemovePrefix(string stateId)
        {
            if (StatePrefix == stateId)
                return null;

            if (StatePrefix == null || !StatePrefix.EndsWith(stateId))
                throw new InvalidOperationException($"Can't remove prefix \"{stateId}\" from \"{StatePrefix}\"");

            return StatePrefix.Substring(0, StatePrefix.Length - stateId.Length - 1);
        }

        private static string AddPrefix(string? prefix, int? recoursionDepth, string stateId)
        {
            if (prefix == null)
                return stateId;

            if (recoursionDepth == null)
                return $"{prefix}.{stateId}";

            return $"{prefix}-{recoursionDepth}.{stateId}";
        }
    }
}
