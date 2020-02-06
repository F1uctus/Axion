﻿using System.Collections.Generic;
using System.Web;
using Axion.Core.Processing.CodeGen;
using Axion.Core.Processing.Syntactic.Expressions.TypeNames;
using Axion.Core.Source;
using Axion.Core.Specification;

namespace Axion.Core.Processing.Lexical.Tokens {
    public class StringToken : Token {
        public bool   IsUnclosed   { get; }
        public string Prefixes     { get; }
        public string Quote        { get; }
        public string EndingQuotes { get; }

        public List<StringInterpolation> Interpolations { get; }

        public          bool     IsMultiline => Quote.Length == 3;
        public override TypeName ValueType   => Spec.StringType;

        internal StringToken(
            SourceUnit                source,
            string                    value          = "",
            string                    content        = "",
            bool                      isUnclosed     = false,
            string                    prefixes       = "",
            string                    quote          = "\"",
            List<StringInterpolation> interpolations = null
        ) : base(source, TokenType.String, value, content) {
            IsUnclosed     = isUnclosed;
            Prefixes       = prefixes;
            Quote          = quote;
            Interpolations = interpolations ?? new List<StringInterpolation>();
            EndingQuotes   = "";
        }

        public bool HasPrefix(string prefix) {
            return Prefixes.Contains(prefix.ToLower())
                || Prefixes.Contains(prefix.ToUpper());
        }

        public override void ToCSharp(CodeWriter c) {
            if (HasPrefix("f")) {
                c.Write("$");
            }

            c.Write("\"", HttpUtility.JavaScriptStringEncode(Content));
            if (!IsUnclosed) {
                c.Write("\"");
            }
        }

        public override void ToPython(CodeWriter c) {
            if (HasPrefix("f")) {
                c.Write("f");
            }

            if (HasPrefix("r")) {
                c.Write("r");
            }

            c.Write(IsMultiline ? "\"\"\"" : "\"");
            c.Write(HttpUtility.JavaScriptStringEncode(Content));
            if (!IsUnclosed) {
                c.Write(IsMultiline ? "\"\"\"" : "\"");
            }
        }
    }

    public class StringInterpolation : Span {
        public StringInterpolation(TextStream stream)
            : base(SourceUnit.FromInterpolation(stream)) { }
    }
}