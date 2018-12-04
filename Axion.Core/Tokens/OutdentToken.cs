﻿namespace Axion.Core.Tokens {
    /// <summary>
    ///     Represents a &lt;indentation decrease&gt; <see cref="Token" />.
    /// </summary>
    public class OutdentToken : Token {
        public OutdentToken((int, int) startPosition)
            : base(TokenType.Outdent, startPosition) {
        }
    }
}