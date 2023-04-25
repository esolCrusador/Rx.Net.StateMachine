namespace Rx.Net.StateMachine.EntityFramework.Tests.Extensions
{
    public struct JoinBuilder<TLeft, TRight>
    {
        public JoinBuilder(IEnumerable<TLeft> left, IEnumerable<TRight> right)
        {
            _left = left;
            _right = right;
        }

        private readonly IEnumerable<TLeft> _left;
        private readonly IEnumerable<TRight> _right;

        public JoinBuilderWithLeftKey<TLeft, TRight, TKey> LeftKey<TKey>(Func<TLeft, TKey> keySelector)
        {
            return new JoinBuilderWithLeftKey<TLeft, TRight, TKey>(_left, _right, keySelector);
        }
    }

    public struct JoinBuilderWithLeftKey<TLeft, TRight, TKey>
    {
        public JoinBuilderWithLeftKey(IEnumerable<TLeft> left, IEnumerable<TRight> right, Func<TLeft, TKey> keySelector)
        {
            _left = left;
            _right = right;
            _keySelector = keySelector;
        }

        private readonly IEnumerable<TLeft> _left;
        private readonly IEnumerable<TRight> _right;
        private readonly Func<TLeft, TKey> _keySelector;

        public JoinBuilder<TLeft, TRight, TKey> RightKey(Func<TRight, TKey> rightKeySelector)
        {
            return new JoinBuilder<TLeft, TRight, TKey>(_left, _right, _keySelector, rightKeySelector);
        }
    }

    public struct JoinBuilder<TLeft, TRight, TKey>
    {
        public JoinBuilder(IEnumerable<TLeft> left, IEnumerable<TRight> right, Func<TLeft, TKey> keySelector, Func<TRight, TKey> rightKeySelector)
        {
            _left = left;
            _right = right;
            _leftKeySelector = keySelector;
            _rightKeySelector = rightKeySelector;
        }

        private readonly IEnumerable<TLeft> _left;
        private readonly IEnumerable<TRight> _right;
        private readonly Func<TLeft, TKey> _leftKeySelector;
        private readonly Func<TRight, TKey> _rightKeySelector;

        public IEnumerable<TResult> LeftJoin<TResult>(TRight? defaultRight, Func<TLeft, TRight?, TResult> getResult)
        {
            return _left.GroupJoin(_right, _leftKeySelector, _rightKeySelector,
                    (left, rigtEnumerable) => rigtEnumerable.DefaultIfEmpty(defaultRight).Select(right => getResult(left, right))
                )
                .SelectMany(rs => rs);
        }

        public IEnumerable<TResult> RightJoin<TResult>(TLeft? defaultLeft, Func<TLeft?, TRight, TResult> getResult)
        {
            return _right.GroupJoin(_left, _rightKeySelector, _leftKeySelector,
                    (right, leftEnumerable) => leftEnumerable.DefaultIfEmpty(defaultLeft).Select(left => getResult(left, right))
                )
                .SelectMany(rs => rs);
        }

        public IEnumerable<TResult> LeftJoin<TResult>(Func<TLeft, TRight?, TResult> getResult)
        {
            return LeftJoin(default, getResult);
        }

        public IEnumerable<TResult> RightJoin<TResult>(Func<TLeft?, TRight, TResult> getResult)
        {
            return RightJoin(default, getResult);
        }

        public IEnumerable<TResult> LeftJoin<TResult>(TRight? defaultRight, Func<TLeft, TRight?, bool, TResult> getResult)
        {
            return _left.GroupJoin(_right, _leftKeySelector, _rightKeySelector,
                    (left, rigtEnumerable) => rigtEnumerable.Select(right => getResult(left, right, true)).DefaultIfEmpty(getResult(left, defaultRight, false))
                )
                .SelectMany(rs => rs);
        }

        public IEnumerable<TResult> LeftJoin<TResult>(Func<TLeft, TRight?, bool, TResult> getResult)
        {
            return LeftJoin(default, getResult);
        }

        public IEnumerable<TResult> Join<TResult>(Func<TLeft, TRight, TResult> getResult)
        {
            return _left.Join(_right, _leftKeySelector, _rightKeySelector, getResult);
        }

        public IEnumerable<TResult> FullOuterJoin<TResult>(TLeft? defaultLeft, TRight? defaultRight, Func<TLeft?, TRight?, TResult> getResult)
        {
            return EnumerableExtensions.FullOuterGroupJoin(_left, _right, _leftKeySelector, _rightKeySelector,
                    (ls, rs) => ls.DefaultIfEmpty(defaultLeft).SelectMany(l => rs.DefaultIfEmpty(defaultRight).Select(r => getResult(l, r))))
                .SelectMany(r => r);
        }

        public IEnumerable<TResult> FullOuterJoin<TResult>(Func<TLeft?, TRight?, TResult> getResult)
        {
            return FullOuterJoin(default, default, getResult);
        }

        public IEnumerable<TResult> LeftGroupJoin<TResult>(Func<TLeft, IEnumerable<TRight>, TResult> getResult)
        {
            return _left.GroupJoin(_right, _leftKeySelector, _rightKeySelector, getResult);
        }

        public MergeExecutor<TLeft, TRight, TKey> Merge()
        {
            return new MergeExecutor<TLeft, TRight, TKey>(_left, _right, _leftKeySelector, _rightKeySelector);
        }
    }
}
