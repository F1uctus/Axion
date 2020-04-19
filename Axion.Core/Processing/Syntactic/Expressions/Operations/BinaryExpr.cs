using Axion.Core.Processing.Lexical.Tokens;
using Axion.Core.Processing.Syntactic.Expressions.Common;

namespace Axion.Core.Processing.Syntactic.Expressions.Operations {
    /// <summary>
    ///     <c>
    ///         binary-expr:
    ///             expr OPERATOR expr;
    ///     </c>
    /// </summary>
    public class BinaryExpr : InfixExpr {
        private Expr left = null!;

        public Expr Left {
            get => left;
            set {
                left = Bind(value);
                MarkStart(left);
            }
        }

        private Expr right = null!;

        public Expr Right {
            get => right;
            set {
                right = Bind(value);
                MarkEnd(right);
            }
        }

        public Token Operator { get; set; }

        public BinaryExpr(Node parent) : base(parent) { }
    }
}
