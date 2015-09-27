# Concepts
### The Plugin Assembly
A plugin assembly contains code that can modify other code.  It contains classes implementing `ICompilerPlugin` and `ICompilerPluginAttribute` in pairs.  Attributes implementing `ICompilerPluginAttribute` can instantiate their paired `ICompilerPlugin` object.  Plugin assemblies must always be separate from the assemblies that they will be modifying, since they are loaded and executed at compile time.

### Loading a Plugin
Projects/assemblies can reference a plugin assembly and apply one or more of its attributes implementing `ICompilerPluginAttribute` to their own assembly.  When the forked Roslyn compiles an assembly, it scans for attributes implementing `ICompilerPluginAttribute` on the compiling assembly.  If they are found, their containing plugin assemblies are loaded using reflection (using the active `IAnalyzerAssemblyLoader`).  Next, their `Create` methods are called, which instantiates their paired `ICompilerPlugin` object.

### Integration with the Roslyn Compilation
#### `BeforeCompile`
Before the main Roslyn compilation starts, `ICompilerPlugin.BeforeCompile(BeforeCompilerContext)` is called on each of the plugins, allowing them to replace the `Compilation` object with a modified version.  This includes syntax modifications.  The plugins are also permitted to add diagnostics (error/warning messages) at this time.

#### `AfterCompile`
After the main Roslyn compilation (and IL emit) has finished, `ICompilerPlugin.AfterCompile(AfterCompilerContext)` is called on each of the plugins, allowing them to inspect (but not replace) the `Compilation` object, add diagnostics, and potentially inspect/modify the generated PE and PDB files (although that has not yet been tested).

#### Fixing Checksums
When a `SyntaxTree` is modified by a plugin, the forked Roslyn will automatically set the checksum of the new `SyntaxTree` to match the old one, making debugging possible (although sometimes line numbers and positions don't line up right).

### Integrating with Visual Studio
Everything is currently integrated into VS by specifying a `CscToolPath` in the assembly that is using plugins which points to the forked Roslyn compiler.  Debugging generated code is difficult if it substantially modifies existing code.  I'm not sure if Intellisense would work properly given the transformed syntax trees, but it will be nice to try things out once the OpenSourceDebug project starts working again.

# How to create a compiler plugin
### Take a look at MacroSharp
My project MacroSharp is the best resource for understanding compiler plugins right now: https://github.com/russpowers/MacroSharp

### Build this fork of Roslyn
- Be sure you have VS2015 installed (Community is ok)
- Clone this repository
- Install the nuget dependencies by opening a command prompt in this folder and typing: `nuget.exe restore Roslyn.sln`
- Open src/Roslyn.sln in VS2015 and build

### Create a plugin project:
- Create a plugin project (class library) with references to Microsoft.CodeAnalysis.dll (built from this repository)
- Add a class that implements ICompilerPlugin
- Add a class that inherits from Attribute and implements ICompilerPluginAttribute

### Create a project to use the plugin
- Create a project to use the plugin project (console, class library, whatever)
- Add a reference to the plugin project
- Modify the project .csproj file by adding <CscToolPath>*ROSLYN PATH*\Binaries\Debug or Release</CscToolPath> to the first <PropertyGroup>. Example: <CscToolPath>D:\roslyn\Binaries\Debug</CscToolPath>
- In any .cs file (only once per assembly though), add an assembly attribute for your compiler plugin attribute you created.  Example: If you created MyPlugins.MyCompilerPluginAttribute, you would have an attribute [assembly:MyPlugins.MyCompilerPlugin]

### Build/run your project
- Note that Visual Studio won't use your plugin for autocomplete, and may show errors where there are none.  To see compiler errors, switch to the Output window.




