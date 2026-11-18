using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace STranslate.Core;

/// <summary>
///     https://github1s.com/Flow-Launcher/Flow.Launcher/blob/dev/Flow.Launcher.Core/Plugin/PluginAssemblyLoader.cs#L11-L53
/// </summary>
public class PluginAssemblyLoader : AssemblyLoadContext
{
    private readonly AssemblyName _assemblyName;
    private readonly AssemblyDependencyResolver _resolver;

    internal PluginAssemblyLoader(string assemblyFilePath)
    {
        _resolver = new AssemblyDependencyResolver(assemblyFilePath);
        _assemblyName = new AssemblyName(Path.GetFileNameWithoutExtension(assemblyFilePath));
    }

    internal Type? FromAssemblyGetTypeOfInterface(Assembly assembly, Type type)
    {
        var allTypes = assembly.ExportedTypes;
        return allTypes.FirstOrDefault(o => o.IsClass && !o.IsAbstract && o.GetInterfaces().Any(t => t == type));
    }

    internal Assembly LoadAssemblyAndDependencies() => LoadFromAssemblyName(_assemblyName);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);

        // When resolving dependencies, ignore assembly depenedencies that already exits with Flow.Launcher
        // Otherwise duplicate assembly will be loaded and some weird behavior will occur, such as WinRT.Runtime.dll
        // will fail due to loading multiple versions in process, each with their own static instance of registration state
        var existAssembly = Default.Assemblies.FirstOrDefault(x => x.FullName == assemblyName.FullName);

        return existAssembly ?? (assemblyPath == null ? null : LoadFromAssemblyPath(assemblyPath));
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return string.IsNullOrEmpty(path) ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }
}