using System.Collections.Generic;
using Axion.Core.Processing.Lexical.Tokens;
using Axion.Core.Processing.Syntax.Tree.Comprehensions;
using Axion.Core.Processing.Syntax.Tree.Expressions;

namespace Axion.Core.Processing.Syntax.Parser {
    public partial class SyntaxParser {
        /// <summary>
        ///     <c>
        ///         comprehension_iterator '}'
        ///     </c>
        /// </summary>
        private SetComprehension FinishSetComprehension(Expression item, Position start) {
            ComprehensionIterator[] iterators = ParseComprehensionIterators();
            stream.Eat(TokenType.RightBrace);

            return new SetComprehension(item, iterators, start, tokenEnd);
        }

        /// <summary>
        ///     <c>
        ///         comprehension_iterator '}'
        ///     </c>
        /// </summary>
        private MapComprehension FinishMapComprehension(Expression key, Expression value, Position start) {
            ComprehensionIterator[] iterators = ParseComprehensionIterators();
            stream.Eat(TokenType.RightBrace);

            return new MapComprehension(key, value, iterators, start, tokenEnd);
        }

        /// <summary>
        ///     <c>
        ///         comprehension_iterator:
        ///         comprehension_for | comprehension_if
        ///     </c>
        /// </summary>
        private ComprehensionIterator[] ParseComprehensionIterators() {
            var              iterators = new List<ComprehensionIterator>();
            ComprehensionFor firstFor  = ParseComprehensionFor();
            iterators.Add(firstFor);

            while (true) {
                if (stream.PeekIs(TokenType.KeywordFor)) {
                    iterators.Add(ParseComprehensionFor());
                }
                else if (stream.PeekIs(TokenType.KeywordIf)) {
                    iterators.Add(ParseComprehensionIf());
                }
                else {
                    break;
                }
            }

            return iterators.ToArray();
        }

        /// <summary>
        ///     <c>
        ///         comprehension_for:
        ///         'for target_list 'in' or_test [comprehension_iterator]
        ///     </c>
        /// </summary>
        private ComprehensionFor ParseComprehensionFor() {
            Token start = StartExprOrStmt(TokenType.KeywordFor);

            List<Expression> target = ParseTargetList(out bool trailingComma);
            Expression       test   = MakeTupleOrExpr(target, trailingComma);

            // expr list is something like:
            // ()
            // a
            // a,b
            // a,b,c
            // we either want just () or a or we want (a,b) and (a,b,c)
            // so we can do tupleExpr.EmitSet() or loneExpr.EmitSet()

            stream.Eat(TokenType.KeywordIn);
            Expression list = ParseOrExpr();
            return new ComprehensionFor(start, test, list);
        }

        /// <summary>
        ///     <c>
        ///         comprehension_if:
        ///         'if' old_test [comprehension_iterator]
        ///     </c>
        /// </summary>
        private ComprehensionIf ParseComprehensionIf() {
            Token      start     = StartExprOrStmt(TokenType.KeywordIf);
            Expression condition = ParseTestExpr();
            return new ComprehensionIf(start, condition);
        }
    }
}