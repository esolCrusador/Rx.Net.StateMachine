using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Rx.Net.StateMachine.Extensions
{
    public class ExpressionRebinder: ExpressionVisitor
    {
        private readonly Dictionary<Expression, Expression> _rebindMap;

        public ExpressionRebinder(Dictionary<Expression, Expression> rebindMap) => 
            _rebindMap = rebindMap;

        public override Expression Visit(Expression node)
        {
            if (node == null)
                return null;

            if (_rebindMap.TryGetValue(node, out var target))
                return target;

            return base.Visit(node);
        }
    }
}
