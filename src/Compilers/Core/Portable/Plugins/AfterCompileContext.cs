using System.Collections.Generic;
using System.IO;

namespace Microsoft.CodeAnalysis.Plugins
{
    /// <summary>
    /// The context passed to compiler plugins after the main compilation has finished.
    /// </summary>
    public class AfterCompileContext
    {
        /// <summary>
        /// The current compilation.  Cannot be modified.
        /// </summary>
        public Compilation Compilation { get; internal set; }

        /// <summary>
        /// A list of diagnostics associated with the current compilation.
        /// Add diagnostics as necessary.
        /// </summary>
        public IList<Diagnostic> Diagnostics { get; internal set; }

        /// <summary>
        /// The PE assembly stream for the current compilation that has been writen.
        /// </summary>
        public Stream AssemblyStream { get; internal set; }

        /// <summary>
        /// The PDB stream for the current compilation that has been writen.
        /// </summary>
        public Stream SymbolStream { get; internal set; }
    }
}
