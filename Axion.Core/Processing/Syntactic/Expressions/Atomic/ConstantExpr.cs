using Axion.Core.Processing.CodeGen;
using Axion.Core.Processing.Lexical.Tokens;
using Axion.Core.Processing.Syntactic.Expressions.Common;
using Axion.Core.Processing.Syntactic.Expressions.TypeNames;
using Axion.Core.Processing.Traversal;

namespace Axion.Core.Processing.Syntactic.Expressions.Atomic {
    /// <summary>
    ///     <c>
    ///         const-expr:
    ///             CONST-TOKEN | STRING+;
    ///     </c>
    /// </summary>
    public class ConstantExpr : AtomExpr {
        public Token Literal { get; set; }

        [NoTraversePath]
        public override TypeName ValueType => Literal.ValueType;

        public static ConstantExpr ParseNew(Expr parent) {
            return new ConstantExpr(parent).Parse();
        }

        public ConstantExpr(
            Expr   parent,
            Token? literal = null
        ) : base(parent) {
            Literal = literal;
        }

        public ConstantExpr(
            Expr   parent,
            string literal
        ) : base(parent) {
            Literal = new Token(Source, value: literal);
        }

        public ConstantExpr Parse() {
            SetSpan(() => { Literal = Stream.EatAny(); });
            return this;
        }

        public override void ToDefault(CodeWriter c) {
            c.Write(Literal);
        }
    }
}