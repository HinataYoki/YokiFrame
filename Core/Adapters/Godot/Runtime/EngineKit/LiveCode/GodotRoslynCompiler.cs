#if GODOT && TOOLS
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace YokiFrame
{
    public static class GodotRoslynCompiler
    {
        public static YokiFrameRoslynCompilerLoader Create(string projectRoot)
        {
            var context = AssemblyLoadContext.GetLoadContext(typeof(GodotRoslynCompiler).Assembly);
            return new YokiFrameRoslynCompilerLoader(projectRoot,
                Path.Combine(projectRoot, ".godot/mono/temp/bin/Debug"), (pe, symbols) =>
                {
                    using var image = new MemoryStream(pe, false);
                    if (symbols == null || symbols.Length == 0) return context.LoadFromStream(image);
                    using var pdb = new MemoryStream(symbols, false);
                    return context.LoadFromStream(image, pdb);
                });
        }
    }
}
#endif
