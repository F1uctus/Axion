namespace Axion.Core.Processing.Syntactic.Expressions.Patterns {
    /// <summary>
    ///     <code>
    ///         syntax-pattern
    ///             : cascade-pattern
    ///             | expression-pattern
    ///             | group-pattern
    ///             | multiple-pattern
    ///             | optional-pattern
    ///             | or-pattern
    ///             | token-pattern;
    ///     </code>
    /// </summary>
    public abstract class Pattern : Node {
        // NOTE: here Match uses parent parameter because
        //  we may want to match macro from another file.
        public abstract bool Match(MacroMatchExpr parent);

        protected Pattern(Node parent) : base(parent) { }
    }
}
