using Axion.Core.Processing.Lexical.Tokens;
using Axion.Core.Processing.Syntactic.Expressions.Common;
using Axion.Core.Processing.Syntactic.Expressions.TypeNames;
using Axion.Specification;
using static Axion.Specification.TokenType;

namespace Axion.Core.Processing.Syntactic.Expressions.Atomic {
    /// <summary>
    ///     <c>
    ///         code-quote-expr:
    ///             '{{' any '}}';
    ///     </c>
    /// </summary>
    public class CodeQuoteExpr : AtomExpr {
        private Token? openQuote;

        public Token? OpenQuote {
            get => openQuote;
            set => openQuote = BindNullable(value);
        }

        private ScopeExpr? scope;

        public ScopeExpr Scope {
            get => scope;
            set => scope = Bind(value);
        }

        private Token? closeQuote;

        public Token? CloseQuote {
            get => closeQuote;
            set => closeQuote = BindNullable(value);
        }

        public override TypeName? ValueType => Scope.ValueType;

        public CodeQuoteExpr(Node parent) : base(parent) { }

        public CodeQuoteExpr Parse() {
            OpenQuote = Stream.Eat(DoubleOpenBrace);
            Scope     = new ScopeExpr(this);
            while (!Stream.PeekIs(DoubleCloseBrace, TokenType.End)) {
                Scope.Items += AnyExpr.Parse(this);
            }
            CloseQuote = Stream.Eat(DoubleCloseBrace);
            return this;
        }
    }
}
