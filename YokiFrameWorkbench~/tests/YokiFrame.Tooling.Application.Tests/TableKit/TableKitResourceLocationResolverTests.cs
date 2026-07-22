using YokiFrame.Tooling.Application.Models.TableKit;
using YokiFrame.Tooling.Application.Services.TableKit;

namespace YokiFrame.Tooling.Application.Tests.TableKit;

/// <summary>验证 TableKit 可寻址开关与跨宿主路径模板推导规则。</summary>
public sealed class TableKitResourceLocationResolverTests
{
    /// <summary>Unity Resources 输出目录推导为 Resources 相对路径。</summary>
    [Fact]
    public void InfersUnityResourcesPath()
    {
        string root = CreateProjectRoot("unity");
        try
        {
            TableKitRuntimeLocation location = new TableKitResourceLocationResolver().Resolve(new TableKitOptions
            {
                ProjectRoot = root,
                LubanConfigPath = Path.Combine(root, "Luban", "luban.conf"),
                OutputDataDir = "Assets/Resources/Art/Table"
            });

            Assert.False(location.IsAddressable);
            Assert.Equal("Art/Table/{0}", location.PathPattern);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>Godot 项目内输出目录推导为 res:// 路径。</summary>
    [Fact]
    public void InfersGodotProjectPath()
    {
        string root = CreateProjectRoot("godot");
        try
        {
            TableKitRuntimeLocation location = new TableKitResourceLocationResolver().Resolve(new TableKitOptions
            {
                ProjectRoot = root,
                LubanConfigPath = Path.Combine(root, "luban.conf"),
                OutputDataDir = "Data/Tables"
            });

            Assert.False(location.IsAddressable);
            Assert.Equal("res://Data/Tables/{0}", location.PathPattern);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>可寻址模式固定按表名读取，自定义模板保留 URI 语义。</summary>
    [Fact]
    public void SupportsAddressableAndCustomPathPatterns()
    {
        string root = CreateProjectRoot("unity");
        try
        {
            TableKitResourceLocationResolver resolver = new();
            TableKitRuntimeLocation addressable = resolver.Resolve(new TableKitOptions
            {
                ProjectRoot = root,
                LubanConfigPath = Path.Combine(root, "luban.conf"),
                IsAddressable = true
            });
            TableKitRuntimeLocation customPath = resolver.Resolve(new TableKitOptions
            {
                ProjectRoot = root,
                LubanConfigPath = Path.Combine(root, "luban.conf"),
                RuntimePathPattern = "user://tables"
            });

            Assert.True(addressable.IsAddressable);
            Assert.Equal("{0}", addressable.PathPattern);
            Assert.False(customPath.IsAddressable);
            Assert.Equal("user://tables/{0}", customPath.PathPattern);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>无法从 Unity 普通 Assets 目录推导运行时定位时明确失败。</summary>
    [Fact]
    public void RejectsUnknownUnityDataDirectory()
    {
        string root = CreateProjectRoot("unity");
        try
        {
            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => new TableKitResourceLocationResolver().Resolve(new TableKitOptions
            {
                ProjectRoot = root,
                LubanConfigPath = Path.Combine(root, "luban.conf"),
                OutputDataDir = "Assets/Generated/Tables"
            }));

            Assert.Contains("开启资源可寻址", exception.Message, StringComparison.Ordinal);
            Assert.Contains("运行时地址模板", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>创建包含 Unity 或 Godot 识别文件的隔离项目。</summary>
    /// <param name="kind">项目类型。</param>
    /// <returns>临时项目根。</returns>
    private static string CreateProjectRoot(string kind)
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-tablekit-location-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        if (kind == "unity")
        {
            Directory.CreateDirectory(Path.Combine(root, "ProjectSettings"));
            File.WriteAllText(Path.Combine(root, "ProjectSettings", "ProjectVersion.txt"), "m_EditorVersion: 2022.3.0f1");
        }
        else
        {
            File.WriteAllText(Path.Combine(root, "project.godot"), "[application]\nconfig/name=\"Game\"\n");
        }
        return root;
    }
}
