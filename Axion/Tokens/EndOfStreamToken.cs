﻿namespace Axion.Tokens {
    /// <summary>
    ///     Represents an &lt;end of input stream&gt; <see cref="Token" />.
    /// </summary>
    public class EndOfStreamToken : Token {
        public EndOfStreamToken((int line, int column) location)
            : base(TokenType.EndOfFile, location, Spec.EndStream.ToString()) {
        }
    }
}