using System.Collections.Generic;
using System.Linq.Expressions;

namespace Rx.Net.StateMachine.Extensions
{
    internal class ExpressionRebinder: ExpressionVisitor
    {
        private readonly Dictionary<Expression, Expression> _rebindMap;

        public ExpressionRebinder(Dictionary<Expression, Expression> rebindMap) => 
            _rebindMap = rebindMap;

        public ExpressionRebinder(Expression from, Expression to) : this(new Dictionary<Expression, Expression> { [from] = to })
        { }

        public override Expression? Visit(Expression? node)
        {
            if (node == null)
                return null;

            if (_rebindMap.TryGetValue(node, out var target))
                return target;

            return base.Visit(node);
        }
    }
}
