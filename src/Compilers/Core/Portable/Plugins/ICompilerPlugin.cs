using System;

namespace Microsoft.CodeAnalysis.Plugins
{

    /// <summary>
    /// To be implemented by compiler plugins.
    /// </summary>
    public interface ICompilerPlugin : IDisposable
    {
        /// <summary>
        /// Called before the main compilation starts.  Use t
        /// </summary>
        /// <param name="context"></param>
        void BeforeCompile(BeforeCompileContext context);
        void AfterCompile(AfterCompileContext context);
    }
}
