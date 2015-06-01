using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.Plugins
{
    /// <summary>
    /// The context passed to compiler plugins before the main compilation has started.
    /// </summary>
    public class BeforeCompileContext
    {
        /// <summary>
        /// The current compilation.  Set this property to modify the compilation.
        /// </summary>
        public Compilation Compilation { get; set; }

        /// <summary>
        /// A list of diagnostics associated with the current compilation.
        /// Add diagnostics as necessary.
        /// </summary>
        public IList<Diagnostic> Diagnostics { get; internal set; }
    }
}
