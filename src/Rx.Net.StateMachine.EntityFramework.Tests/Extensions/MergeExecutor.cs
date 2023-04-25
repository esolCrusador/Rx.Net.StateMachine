namespace Rx.Net.StateMachine.EntityFramework.Tests.Extensions
{
    public class MergeExecutor<TLeft, TRight, TKey>
    {
        private readonly IEnumerable<TLeft> _left;
        private readonly IEnumerable<TRight> _right;
        private readonly Func<TLeft, TKey> _leftKey;
        private readonly Func<TRight, TKey> _rightKey;

        private Action<TRight>? _create;
        private Action<TLeft, TRight>? _update;
        private Action<TLeft>? _delete;

        private Func<TLeft, TRight, bool>? _isChanged;

        private Action<IReadOnlyList<TRight>>? _creatyMany;
        private Action<IReadOnlyList<TLeft>>? _deleteMany;

        public MergeExecutor(IEnumerable<TLeft> left, IEnumerable<TRight> right, Func<TLeft, TKey> leftKey, Func<TRight, TKey> rightKey)
        {
            _left = left;
            _right = right;
            _leftKey = leftKey;
            _rightKey = rightKey;
        }

        public MergeExecutor<TLeft, TRight, TKey> IsChanged(Func<TLeft?, TRight?, bool> isChanged)
        {
            _isChanged = isChanged;

            return this;
        }

        public MergeExecutor<TLeft, TRight, TKey> Update(Action<TLeft, TRight> update)
        {
            _update = update;

            return this;
        }

        public MergeExecutor<TLeft, TRight, TKey> Create(Action<TRight> create)
        {
            _create = create;

            return this;
        }

        public MergeExecutor<TLeft, TRight, TKey> CreateMany(Action<IReadOnlyList<TRight>> createMany)
        {
            _creatyMany = createMany;

            return this;
        }

        public MergeExecutor<TLeft, TRight, TKey> Delete(Action<TLeft> delete)
        {
            _delete = delete;

            return this;
        }

        public MergeExecutor<TLeft, TRight, TKey> DeleteMany(Action<IReadOnlyList<TLeft>> deleteMany)
        {
            _deleteMany = deleteMany;

            return this;
        }

        public void Execute(List<TLeft>? missingInRight = null, List<TRight>? missingInLeft = null)
        {
            var joinResult = EnumerableExtensions.FullOuterGroupJoin(_left, _right, _leftKey, _rightKey,
                (leftResults, rightResults) =>
                    new
                    {
                        Entities = leftResults,
                        Models = rightResults
                    });

            if (missingInRight == null && _deleteMany != null)
                missingInRight = new List<TLeft>();

            if (missingInLeft == null && _creatyMany != null)
                missingInLeft = new List<TRight>();

            foreach (var pair in joinResult)
            {
                bool hasEntity = pair.Entities.TryFirst(out TLeft? entity);
                if (hasEntity)
                {
                    TLeft entityValue = entity ?? throw new ArgumentException(nameof(entity));
                    bool hasModel = pair.Models.TryFirst(out TRight? model);

                    if (hasModel)
                    {
                        TRight modelValue = model ?? throw new ArgumentException(nameof(model));
                        if (_isChanged == null || _isChanged(entityValue, modelValue))
                            _update?.Invoke(entityValue, modelValue);
                    }
                    else
                    {
                        missingInRight?.Add(entityValue);
                        _delete?.Invoke(entityValue);
                    }
                }
                else
                {
                    foreach (TRight pairModel in pair.Models)
                    {
                        missingInLeft?.Add(pairModel);

                        _create?.Invoke(pairModel);
                    }
                }
            }

            if (missingInLeft != null)
                _creatyMany?.Invoke(missingInLeft);

            if (missingInRight != null)
                _deleteMany?.Invoke(missingInRight);
        }
    }
}
