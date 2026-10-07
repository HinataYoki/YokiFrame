#if GODOT && TOOLS
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Godot;

namespace YokiFrame
{
    /// <summary>
    /// 为 Godot Tools 建立内存编译器。编译器 bundle 先找已安装 add-on，再由加载器回退项目缓存。
    /// 脚本引用从当前已加载程序集取得，不绑定 Windows 或单一构建配置。
    /// </summary>
    public static class GodotRoslynCompiler
    {
        /// <summary>Godot 安装投影中的包根。</summary>
        private const string PACKAGE_RESOURCE = "res://addons/yokiframe/package/YokiFrame/";

        /// <summary>
        /// 创建绑定当前加载上下文的编译器。用户脚本加载进宿主上下文，不能卸载。
        /// </summary>
        /// <param name="projectRoot">Godot 工程根。</param>
        /// <returns>使用当前程序集加载上下文的编译器。</returns>
        public static RoslynCompilerLoader Create(string projectRoot)
        {
            var context = AssemblyLoadContext.GetLoadContext(typeof(GodotRoslynCompiler).Assembly);
            return new RoslynCompilerLoader(projectRoot, CompilerDirectories(), null, (pe, symbols) =>
            {
                using var image = new MemoryStream(pe, false);
                if (symbols == null || symbols.Length == 0) return context.LoadFromStream(image);
                using var pdb = new MemoryStream(symbols, false);
                return context.LoadFromStream(image, pdb);
            });
        }

        /// <summary>返回 Godot 安装后可见的编译器目录。不存在时由加载器继续查找项目缓存。</summary>
        /// <returns>候选目录。</returns>
        private static IReadOnlyList<string> CompilerDirectories()
        {
            var directories = new List<string>();
            string resource = PACKAGE_RESOURCE + RoslynCompilerLoader.PACKAGE_RELATIVE_PATH;
            string packaged = ProjectSettings.GlobalizePath(resource);
            if (!string.IsNullOrEmpty(packaged) && packaged != resource) directories.Add(packaged);
            return directories;
        }
    }
}
#endif
