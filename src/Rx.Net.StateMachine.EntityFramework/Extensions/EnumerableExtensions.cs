using Rx.Net.StateMachine.EntityFramework.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Rx.Net.StateMachine.EntityFramework.Extensions
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<TResult> FullOuterGroupJoin<TLeft, TRight, TKey, TResult>(
            IEnumerable<TLeft> leftEnumerable,
            IEnumerable<TRight> rightEnumerable,
            Func<TLeft, TKey> leftKeySelector,
            Func<TRight, TKey> rightKeySelector,
            Func<IEnumerable<TLeft>, IEnumerable<TRight>, TResult> resultSelector,
            EqualityComparer<TKey>? comparer = null)
        {
            if (comparer == null)
                comparer = EqualityComparer<TKey>.Default;

            ILookup<TKey, TLeft> leftLookup = leftEnumerable.ToLookup(leftKeySelector, comparer);
            ILookup<TKey, TRight> rightLookup = rightEnumerable.ToLookup(rightKeySelector, comparer);

            foreach (IGrouping<TKey, TLeft> left in leftLookup)
            {
                yield return resultSelector(left, rightLookup[left.Key]);
            }

            foreach (IGrouping<TKey, TRight> right in rightLookup.Where(right => !leftLookup.Contains(right.Key)))
            {
                yield return resultSelector(Enumerable.Empty<TLeft>(), right);
            }
        }

        public static JoinBuilder<TLeft, TRight> JoinTo<TLeft, TRight>(this IEnumerable<TLeft> source, IEnumerable<TRight> right)
        {
            return new JoinBuilder<TLeft, TRight>(source, right);
        }

        public static MergeExecutor<KeyValuePair<int, TLeft>, KeyValuePair<int, TRight>, int> ZipMerge<TLeft, TRight>(this IEnumerable<TLeft> source, IEnumerable<TRight> right)
        {
            return source.Select((l, i) => new KeyValuePair<int, TLeft>(i, l))
                .JoinTo(right.Select((r, i) => new KeyValuePair<int, TRight>(i, r)))
                .LeftKey(kvp => kvp.Key)
                .RightKey(kvp => kvp.Key)
                .Merge();
        }

        public static bool TryFirst<TElement>(this IEnumerable<TElement> source, [MaybeNullWhen(false)] out TElement? element)
        {
            using (IEnumerator<TElement> enumerator = source.GetEnumerator())
            {
                if (enumerator.MoveNext())
                {
                    element = enumerator.Current;
                    return true;
                }
                else
                {
                    element = default;
                    return false;
                }
            }
        }

        public static IEnumerable<TElement> Concat<TElement>(this IEnumerable<TElement> elements, TElement oneMore)
        {
            foreach (TElement el in elements)
            {
                yield return el;
            }

            yield return oneMore;
        }

        public static TElement? MaxBy<TElement, TValue>(this IEnumerable<TElement> elements, Func<TElement, TValue> selector, Comparer<TValue>? comparer = null)
        {
            return BestBy(elements, selector, comparer ?? Comparer<TValue>.Default, 1);
        }

        public static TElement? MinBy<TElement, TValue>(this IEnumerable<TElement> elements, Func<TElement, TValue> selector, Comparer<TValue>? comparer = null)
        {
            return BestBy(elements, selector, comparer ?? Comparer<TValue>.Default, -1);
        }

        public static IQueryable<TElement> ConcatAll<TElement>(this IEnumerable<IQueryable<TElement>> elementQueries)
        {
            IQueryable<TElement>? result = null;
            foreach (var query in elementQueries)
            {
                if (result == null)
                    result = query;
                else
                    result = result.Concat(query);
            }

            return result ?? Enumerable.Empty<TElement>().AsQueryable();
        }

        public static IEnumerable<TSource[]> Batch<TSource>(this IEnumerable<TSource> source, int size)
        {
            TSource[]? bucket = null;
            var count = 0;

            foreach (var item in source)
            {
                if (bucket == null)
                    bucket = new TSource[size];

                bucket[count++] = item;
                if (count != size)
                    continue;

                yield return bucket;

                bucket = null;
                count = 0;
            }

            if (bucket != null && count > 0)
                yield return bucket.Take(count).ToArray();
        }

        public static IEnumerable<IReadOnlyCollection<TSource>> BatchGroups<TSource>(this IEnumerable<IReadOnlyCollection<TSource>> groups, int size)
        {
            List<TSource>? bucket = null;

            foreach (var items in groups)
            {
                if ((bucket?.Count ?? 0) + items.Count > size && bucket != null)
                {
                    yield return bucket;
                    bucket = null;
                }

                foreach (var item in items)
                {
                    (bucket ??= new List<TSource>()).Add(item);
                    if (bucket.Count != size)
                        continue;

                    yield return bucket;
                    bucket = null;
                }
            }

            if (bucket != null && bucket.Count > 0)
                yield return bucket;
        }

        private static TElement? BestBy<TElement, TValue>(IEnumerable<TElement> elements, Func<TElement, TValue> selector, Comparer<TValue> comparer, int isBetter)
        {
            if (comparer == null)
                comparer = Comparer<TValue>.Default;

            TElement maxElement;
            using (var enumerator = elements.GetEnumerator())
            {
                if (!enumerator.MoveNext())
                    return default;

                maxElement = enumerator.Current;
                TValue maxValue = selector(maxElement);

                while (enumerator.MoveNext())
                {
                    TElement currentElement = enumerator.Current;
                    TValue currentValue = selector(currentElement);

                    if (comparer.Compare(currentValue, maxValue) == isBetter)
                    {
                        maxElement = currentElement;
                        maxValue = currentValue;
                    }
                }
            }

            return maxElement;
        }

        //public static IEnumerable<T> Flatten<T>(this T root, Func<T, IEnumerable<T>> f) =>
        //    f(root).SelectMany(x => x.Flatten(f)).Prepend(root);

        //public static IEnumerable<T> Flatten<T>(this IEnumerable<T> e, Func<T, IEnumerable<T>> f) =>
        //    e.SelectMany(x => x.Flatten(f));

        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> elements)
            where TKey : notnull
        {
            return elements.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }
}
