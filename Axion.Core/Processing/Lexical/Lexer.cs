using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Axion.Core.Processing.Errors;
using Axion.Core.Processing.Lexical.Tokens;
using Axion.Core.Processing.Source;
using Axion.Core.Specification;
using static Axion.Core.Specification.Spec;
using static Axion.Core.Specification.TokenType;

namespace Axion.Core.Processing.Lexical {
    /// <summary>
    ///     Tool for splitting Axion
    ///     code into list of tokens.
    /// </summary>
    public partial class Lexer {
        #region Properties

        /// <summary>
        ///     Current processing source unit.
        /// </summary>
        private readonly SourceUnit unit;

        /// <summary>
        ///     Resulting tokens list.
        /// </summary>
        private readonly List<Token> tokens;

        /// <summary>
        ///     All unclosed multiline comments found in source.
        /// </summary>
        private readonly Stack<CommentToken> unclosedMultilineComments = new Stack<CommentToken>();

        /// <summary>
        ///     All unclosed strings found in source.
        /// </summary>
        private readonly Stack<StringToken> unclosedStrings = new Stack<StringToken>();

        /// <summary>
        ///     All unpaired '{', '[', '(' found in source.
        /// </summary>
        private readonly Stack<Token> mismatchingPairs = new Stack<Token>();

        /// <summary>
        ///     Values, from which lexer should stop working.
        /// </summary>
        private readonly List<string> processCancellers = new List<string>();

        #region Current token properties

        /// <summary>
        ///     Value of current reading token.
        /// </summary>
        private readonly StringBuilder tokenValue = new StringBuilder();

        /// <summary>
        ///     Start position of current reading token.
        /// </summary>
        private Position tokenStartPosition = (0, 0);

        #endregion

        #endregion

        public Lexer(SourceUnit sourceUnit) {
            unit   = sourceUnit;
            tokens = sourceUnit.Tokens;
        }

        /// <summary>
        ///     Private constructor for
        ///     string interpolations reading.
        /// </summary>
        private Lexer(
            SourceUnit  sourceUnit,
            int         charI,
            int         lineI,
            int         columnI,
            List<Token> outTokens
        ) {
            unit      = sourceUnit;
            charIdx   = charI;
            lineIdx   = lineI;
            columnIdx = columnI;
            tokens    = outTokens ?? new List<Token>();
        }

        /// <summary>
        ///     Divides code into list of tokens.
        /// </summary>
        public void Process() {
            if (string.IsNullOrWhiteSpace(unit.Code)) {
                return;
            }

            Token token = null;
            // at first, check if we're after unclosed string
            if (unclosedStrings.Count > 0) {
                token = ReadString(true);
            }

            // then, check if we're after unclosed multiline comment
            if (unclosedMultilineComments.Count > 0) {
                token = ReadMultilineComment();
            }

            if (token == null) {
                token = ReadNextToken();
            }

            while (true) {
                if (token != null) {
                    tokens.Add(token);
                    // check for processing terminator
                    if (token.Is(End) || processCancellers.Contains(token.Value)) {
                        break;
                    }
                }

                token = ReadNextToken();
            }

            #region Process mismatches

            foreach (Token mismatch in mismatchingPairs) {
                BlameType errorType;
                switch (mismatch.Type) {
                case OpenParenthesis:
                case CloseParenthesis: {
                    errorType = BlameType.MismatchedParenthesis;
                    break;
                }

                case OpenBracket:
                case CloseBracket: {
                    errorType = BlameType.MismatchedBracket;
                    break;
                }

                case OpenBrace:
                case CloseBrace: {
                    errorType = BlameType.MismatchedBrace;
                    break;
                }

                default: {
                    throw new Exception(
                        "Internal error: "
                      + nameof(mismatchingPairs)
                      + " grabbed invalid "
                      + nameof(TokenType)
                      + ": "
                      + mismatch.Type
                    );
                }
                }

                unit.Blame(errorType, mismatch.Span.Start, mismatch.Span.End);
            }

            #endregion
        }

        private Token ReadNextToken() {
            // reset token properties
            tokenStartPosition = Position;
            tokenValue.Clear();

#if DEBUG
            if (tokens.Count != 0) {
                Token last = tokens.Last();
                if (last.Type != Outdent) {
                    Debug.Assert(
                        tokenStartPosition
                     == (last.Span.End.Line,
                         last.Span.End.Column + last.EndWhitespaces.Length)
                    );
                }
            }
#endif

            if (c == '\r') {
                tokenValue.Append('\r');
                Move();
                return ReadNewline();
            }

            if (c == '\n') {
                return ReadNewline();
            }

            if (c == EndOfCode) {
                return new Token(End, tokenStartPosition);
            }

            if (NextIs(White)) {
                return ReadWhite();
            }

            if (NextIs(MultiCommentStart)) {
                return ReadMultilineComment();
            }

            if (NextIs(CommentStart)) {
                return ReadSingleLineComment();
            }

            if (NextIs(CharQuotes)) {
                return ReadCharLiteral();
            }

            if (NextIs(StringQuotes)) {
                return ReadString(false);
            }

            if (IsNumberStart(c)) {
                return ReadNumberStart();
            }

            if (IsIdStart(c)) {
                return ReadWord();
            }

            if (NextIs(SymbolicChars)) {
                return ReadSymbolic();
            }

            // invalid
            tokenValue.Append(c);
            unit.Blame(BlameType.InvalidCharacter, tokenStartPosition, Position);
            Move();
            return new Token(Invalid, tokenValue.ToString(), tokenStartPosition);
        }

        /// <summary>
        ///     Gets a language keyword or identifier
        ///     from next piece of source.
        /// </summary>
        private Token ReadWord() {
            do {
                tokenValue.Append(c);
                Move();
            } while (IsIdPart(c)
                  || c == '-'
                  && IsIdPart(Peek));

            string value = tokenValue.ToString();
            // for operators, written as words
            if (Operators.ContainsKey(value)) {
                return new OperatorToken(value, tokenStartPosition);
            }

            return new WordToken(value, tokenStartPosition);
        }

        private Token ReadSymbolic() {
            int    longestLength = SortedSymbolics[0].Length;
            string nextCodePiece = PeekNext(longestLength);
            var    value         = "";
            Token  result        = null;
            for (int length = nextCodePiece.Length; length > 0; length--) {
                value = nextCodePiece.Substring(0, length);
                // grow sequence of characters
                if (!SortedSymbolics.Contains(value)) {
                    continue;
                }

                if (Operators.ContainsKey(value)) {
                    result = new OperatorToken(value, tokenStartPosition);
                    break;
                }

                if (Symbols.ContainsKey(value)) {
                    result = new SymbolToken(value, tokenStartPosition);
                    // yet unclosed bracket
                    if (result.Type.IsOpenBracket()) {
                        mismatchingPairs.Push(result);
                    }
                    else if (result.Type.IsCloseBracket()) {
                        // unopened close bracket
                        if (mismatchingPairs.Count == 0) {
                            mismatchingPairs.Push(result);
                        }
                        // matching bracket
                        else {
                            mismatchingPairs.Pop();
                        }
                    }

                    break;
                }
            }

            Move(value.Length);
            if (result == null) {
                // create unknown symbol
                result = new Token(Unknown, value, tokenStartPosition);
                // not found in specification
                // unit.Blame(BlameType.InvalidOperator, tokenStartPosition, Position);
            }

            return result;
        }
    }
}