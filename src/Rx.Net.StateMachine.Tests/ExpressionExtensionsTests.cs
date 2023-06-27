using FluentAssertions;
using Rx.Net.StateMachine.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Rx.Net.StateMachine.Tests
{
    [Trait("Category", "Fast")]
    public class ExpressionExtensionsTests
    {
        [Theory]
        [InlineData("ab", "aa", "aaab")]
        [InlineData("abb", "aagg", "aa", "aaabt", "ba", "aabb")]
        public void Should_Combine_AndAlso_Expressions(params string[] input)
        {
            List<Expression<Func<string, bool>>> filters = new List<Expression<Func<string, bool>>>
            {
                n => n.StartsWith("a"),
                n => n.EndsWith("b")
            };
            var combinedExpression = ExpressionExtensions.Aggregate(filters, (val1, val2) => val1 && val2);
            var combinedFilter = combinedExpression.Compile();

            var result = input.Where(combinedFilter);

            IEnumerable<string> expected = input.Where(v => filters.All(filter => filter.Compile()(v)));

            result.Should().BeEquivalentTo(expected);
        }

        [Theory]
        [InlineData("ab", "aa", "aaab", "t", "r")]
        [InlineData("abb", "aagg", "aa", "aaabt", "ba", "aabb")]
        [InlineData("a", "b", "c")]
        public void Should_Combine_OrElse_Expressions(params string[] input)
        {
            List<Expression<Func<string, bool>>> filters = new List<Expression<Func<string, bool>>>
            {
                n => n.StartsWith("a"),
                n => n.EndsWith("b")
            };
            var combinedExpression = ExpressionExtensions.Aggregate(filters, (val1, val2) => val1 || val2);
            var combinedFilter = combinedExpression.Compile();

            var result = input.Where(combinedFilter);

            IEnumerable<string> expected = input;
            expected = expected.Where(v => filters.Any(filter => filter.Compile()(v)));

            result.Should().BeEquivalentTo(expected);
        }
    }
}
