﻿using System;

namespace Axion.Core.Processing {
    /// <summary>
    ///     Defines Options of processing of source code.
    /// </summary>
    [Flags]
    public enum SourceProcessingOptions {
        /// <summary>
        ///     Source is processed by default.
        /// </summary>
        None = 0,

        /// <summary>
        ///     Code should be checked against
        ///     inconsistent indentation (mixed spaced and tabs).
        /// </summary>
        CheckIndentationConsistency = 1 << 0,

        /// <summary>
        ///     Compiler should save debugging information
        ///     to files when performing code syntax analysis.
        /// </summary>
        SyntaxAnalysisDebugOutput = 1 << 1
    }
}