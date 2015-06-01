using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.Plugins
{
    /// <summary>
    /// An assembly attribute should implement this interface to be used as
    /// a compiler plugin.
    /// </summary>
    public interface ICompilerPluginAttribute
    {
        /// <summary>
        /// Create a new compiler plugin.
        /// </summary>
        ICompilerPlugin Create();
    }
}
