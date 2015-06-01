using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Microsoft.CodeAnalysis.Plugins
{
    class CompilerPluginExecutor : IDisposable
    {
        private const string ICompilerPluginAttributeName = "Microsoft.CodeAnalysis.Plugins.ICompilerPluginAttribute";

        private static readonly DiagnosticDescriptor s_compilerPluginException =
           new DiagnosticDescriptor("CP1001", "Compiler plugin exception", 
               "Plugin exception thrown from {0}.  Full exception: {1}", "Plugin", DiagnosticSeverity.Error, true);
        private readonly IAnalyzerAssemblyLoader _assemblyLoader;
        private List<AttributeData> _pluginAttributes;
        private List<ICompilerPlugin> _plugins;

        public Compilation Compilation { get; private set; }
        public IList<Diagnostic> Diagnostics { get; private set; }

        public CompilerPluginExecutor(IAnalyzerAssemblyLoader assemblyLoader)
        {
            _assemblyLoader = assemblyLoader;
            Diagnostics = new List<Diagnostic>();
        }

        /// <summary>
        /// Executes the <see cref="ICompilerPlugin.BeforeCompile(BeforeCompileContext)"/> on all plugins.
        /// Should be called just after the compilation has been created.
        /// The <see cref="Compilation"/> property contains the resulting compilation, and 
        /// <see cref="Diagnostics"/> property contains the resulting Diagnostic items.
        /// </summary>
        /// <param name="compilation">The current compilation.</param>
        public void ExecuteBeforeCompile(Compilation compilation)
        {
            Compilation = compilation;
            Diagnostics.Clear();

            if (!InstantiatePlugins())
            {
                return;
            }

            var originalSyntaxTrees = new Dictionary<string, SyntaxTree>();

            foreach (var syntaxTree in Compilation.SyntaxTrees)
                originalSyntaxTrees.Add(syntaxTree.FilePath, syntaxTree);

            var beforeContext = new BeforeCompileContext
            {
                Compilation = Compilation,
                Diagnostics = Diagnostics
            };

            foreach (var plugin in _plugins)
            {
                try
                {
                    plugin.BeforeCompile(beforeContext);
                }
                catch (Exception e)
                {
                    AddExceptionDiagnostic(plugin.GetType().FullName, e);
                    return;
                }
            }

            Compilation = beforeContext.Compilation ?? Compilation;

            foreach (var syntaxTree in Compilation.SyntaxTrees)
            {
                SyntaxTree originalSyntaxTree;
                if (originalSyntaxTrees.TryGetValue(syntaxTree.FilePath, out originalSyntaxTree))
                {
                    syntaxTree.ForceChecksum(originalSyntaxTree.GetText().GetChecksum());
                }
            }
        }

        /// <summary>
        /// Executes the <see cref="ICompilerPlugin.AfterCompile(AfterCompileContext)"/> on all plugins.
        /// Should be called at the end of the compilation process.
        /// The <see cref="Diagnostics"/> property contains the resulting Diagnostic items.
        /// </summary>
        /// <param name="compilation">The current compilation.</param>
        /// <param name="assemblyStream">The PE output stream, already written and still open.</param>
        /// <param name="symbolStream">The PDB output stream, already written and still open.</param>
        public void ExecuteAfterCompile(Compilation compilation, Stream assemblyStream, Stream symbolStream)
        {
            Compilation = compilation;
            Diagnostics.Clear();

            if (_plugins == null)
            {
                return;
            }

            var afterContext = new AfterCompileContext
            {
                Compilation = Compilation,
                Diagnostics = Diagnostics,
                AssemblyStream = assemblyStream,
                SymbolStream = symbolStream
            };

            foreach (var plugin in _plugins)
            {
                try
                {
                    plugin.AfterCompile(afterContext);
                }
                catch (Exception e)
                {
                    AddExceptionDiagnostic(plugin.GetType().FullName, e);
                    return;
                }
            }
        }

        public void Dispose()
        {
            if (_plugins == null)
            {
                return;
            }

            foreach (var plugin in _plugins)
            {
                plugin.Dispose();
            }
        }

        /// <summary>
        /// Finds any plugin attributes defined on the compiling assembly and instantiates them.
        /// </summary>
        private bool InstantiatePlugins()
        {
            Compilation compilationWithRoslynRef;

            var pluginBaseClass = Compilation.GetTypeByMetadataName(ICompilerPluginAttributeName);

            // If we can't find the compiler plugin attribute defined in this assembly or its references, 
            // create a new compilation that includes a reference to Microsoft.CodeAnalysis.
            // This allows assemblies using compiler plugins to not have references to Roslyn.
            if (pluginBaseClass == null)
            {
                var currentAssembly = typeof(CompilerPluginExecutor).GetTypeInfo().Assembly;
                var mdRef = MetadataReference.CreateFromAssembly(currentAssembly);
                compilationWithRoslynRef = Compilation.AddReferences(mdRef);
                pluginBaseClass = compilationWithRoslynRef.GetTypeByMetadataName(ICompilerPluginAttributeName);
            }
            else
            {
                compilationWithRoslynRef = Compilation;
            }

            _pluginAttributes = GetPluginAttributes(compilationWithRoslynRef, pluginBaseClass);

            if (_pluginAttributes == null)
            {
                return false;
            }

            _plugins = new List<ICompilerPlugin>(_pluginAttributes.Count);

            foreach (var pluginAttribute in _pluginAttributes)
            {
                try
                {
                    _plugins.Add(InstantiatePlugin(compilationWithRoslynRef, pluginAttribute));
                }
                catch (Exception e)
                {
                    INamedTypeSymbol attributeClass = pluginAttribute.AttributeClass;
                    string attributeName = attributeClass.ToDisplayString(SymbolDisplayFormat.QualifiedNameOnlyFormat);
                    AddExceptionDiagnostic(attributeName, e);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Instantiates a single plugin based on an attribute that implements <see cref="ICompilerPluginAttribute"/>.
        /// </summary>
        private ICompilerPlugin InstantiatePlugin(Compilation compilation, AttributeData attribute)
        {
            INamedTypeSymbol attributeClass = attribute.AttributeClass;
            string assemblyPath = GetAssemblyPath(compilation, attributeClass.ContainingAssembly);
            Assembly assembly = _assemblyLoader.LoadFromPath(assemblyPath);
            string attributeName = attributeClass.ToDisplayString(SymbolDisplayFormat.QualifiedNameOnlyFormat);
            Type attributeType = assembly.GetType(attributeName);
            var instance = (ICompilerPluginAttribute)Activator.CreateInstance(attributeType);
            return instance.Create();
        }

        /// <summary>
        /// Gets all assembly attributes in the compiling assembly that implement <see cref="ICompilerPluginAttribute"/>.
        /// </summary>
        /// <param name="compilation"></param>
        /// <param name="pluginInterface"></param>
        /// <returns></returns>
        private List<AttributeData> GetPluginAttributes(Compilation compilation, INamedTypeSymbol pluginInterface)
        {
            List<AttributeData> lazyAttributes = null;

            var assembly = compilation.Assembly;
            foreach (var attribute in assembly.GetAttributes())
            {
                foreach (var intf in attribute.AttributeClass.AllInterfaces)
                {
                    if (intf == pluginInterface)
                    {
                        if (lazyAttributes == null)
                            lazyAttributes = new List<AttributeData>();

                        lazyAttributes.Add(attribute);
                        break;
                    }
                }
            }

            return lazyAttributes;
        }

        private string GetAssemblyPath(Compilation compilation, IAssemblySymbol assemblySymbol)
        {
            return compilation.GetMetadataReference(assemblySymbol).Display;
        }

        private void AddExceptionDiagnostic(string pluginName, Exception exception)
        {
            var diagnostic =
                Diagnostic.Create(
                    s_compilerPluginException, Location.None, pluginName, exception.ToString());

            Diagnostics.Add(diagnostic);
        }
    }
}
