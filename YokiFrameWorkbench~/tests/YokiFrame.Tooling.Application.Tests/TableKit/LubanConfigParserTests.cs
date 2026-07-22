using YokiFrame.Tooling.Application.Models.TableKit;
using YokiFrame.Tooling.Application.Services.TableKit;

namespace YokiFrame.Tooling.Application.Tests.TableKit;

/// <summary>验证 TableKit 对动态 namespace、manager 和数据 target 的解析契约。</summary>
public sealed class LubanConfigParserTests
{
    /// <summary>锁定可寻址开关、自动路径默认值与生成开关。</summary>
    [Fact]
    public void UsesWorkbenchDefaults()
    {
        TableKitOptions options = new()
        {
            ProjectRoot = "project",
            LubanConfigPath = "project/Luban/MiniTemplate/luban.conf"
        };

        Assert.Equal("Assets/Resources/Art/Table/", options.OutputDataDir);
        Assert.Equal("Assets/Scripts/TableKit/", options.OutputCodeDir);
        Assert.False(options.IsAddressable);
        Assert.Empty(options.RuntimePathPattern);
        Assert.False(options.UseAssemblyDefinition);
        Assert.False(options.GenerateExternalTypeUtil);
        Assert.False(options.UseAsyncLoading);
        Assert.True(options.UseRawResourceLoading);
        Assert.Equal("YokiFrame.TableKit", options.AssemblyName);
    }

    /// <summary>自定义 topModule/manager 不应回退到旧版 cfg.Tables。</summary>
    [Fact]
    public void ParsesCustomTopModuleAndManager()
    {
        string root = CreateTempDirectory();
        string config = Path.Combine(root, "luban.conf");
        File.WriteAllText(config, "{\"targets\":[{\"name\":\"client\",\"topModule\":\"Game.Config\",\"manager\":\"InventoryTables\"}]}");
        TableKitContract contract = new LubanConfigParser().Parse(new TableKitOptions
        {
            ProjectRoot = root,
            LubanConfigPath = config,
            TargetName = "client",
            DataTarget = "protobuf3-json"
        });

        Assert.Equal("Game.Config.InventoryTables", contract.TablesType);
        Assert.Equal("json", contract.DataExtension);
    }

    /// <summary>按 schemaFiles 中的 mapper 匹配 target/codeTarget，并保留自定义 helper 名称。</summary>
    [Fact]
    public void ParsesExternalTypeMappingsFromSchemaFiles()
    {
        string root = CreateTempDirectory();
        string defines = Path.Combine(root, "Defines");
        Directory.CreateDirectory(defines);
        string config = Path.Combine(root, "luban.conf");
        File.WriteAllText(
            config,
            "{\"schemaFiles\":[{\"fileName\":\"Defines\",\"type\":\"\"}],\"targets\":[{\"name\":\"client\",\"topModule\":\"cfg\",\"manager\":\"Tables\"}]}");
        File.WriteAllText(
            Path.Combine(defines, "builtin.xml"),
            """
            <module name="">
              <bean name="point" valueType="1">
                <var name="x" type="float"/>
                <var name="displayName" type="string"/>
                <mapper target="client,server" codeTarget="cs-bin,cs-dotnet-json">
                  <option name="type" value="Game.Math.Point"/>
                  <option name="constructor" value="ConfiguredTypeMapper.CreatePoint"/>
                </mapper>
                <mapper target="server" codeTarget="cs-bin">
                  <option name="type" value="Server.Point"/>
                  <option name="constructor" value="ServerTypeMapper.CreatePoint"/>
                </mapper>
              </bean>
            </module>
            """);

        TableKitContract contract = new LubanConfigParser().Parse(new TableKitOptions
        {
            ProjectRoot = root,
            LubanConfigPath = config,
            GenerateExternalTypeUtil = true,
            TargetName = "client",
            CodeTarget = "cs-bin"
        });

        TableKitExternalTypeMapping mapping = Assert.Single(contract.ExternalTypeMappings);
        Assert.Equal("global::cfg.point", mapping.SourceTypeName);
        Assert.Equal("Game.Math.Point", mapping.TargetTypeName);
        Assert.Equal("cfg", mapping.HelperNamespace);
        Assert.Equal("ConfiguredTypeMapper", mapping.HelperTypeName);
        Assert.Equal("CreatePoint", mapping.HelperMethodName);
        Assert.Equal(new[] { "X", "DisplayName" }, mapping.MemberNames);
    }

    /// <summary>解析任意未知 data target 时保留 target 后缀而不是强制 binary/json。</summary>
    [Fact]
    public void KeepsUnknownDataTargetExtension()
    {
        string root = CreateTempDirectory();
        string config = Path.Combine(root, "luban.conf");
        File.WriteAllText(config, "{\"targets\":[{\"name\":\"client\",\"topModule\":\"Cfg\",\"manager\":\"Tables\"}]}");
        TableKitContract contract = new LubanConfigParser().Parse(new TableKitOptions
        {
            ProjectRoot = root,
            LubanConfigPath = config,
            DataTarget = "flatbuffers-json"
        });

        Assert.Equal("json", contract.DataExtension);
    }

    /// <summary>拒绝可越出 TableKit 根目录的程序集名，避免生成宿主项目文件时发生路径逃逸。</summary>
    [Fact]
    public void RejectsInvalidAssemblyName()
    {
        string root = CreateTempDirectory();
        string config = Path.Combine(root, "luban.conf");
        File.WriteAllText(config, "{\"targets\":[{\"name\":\"client\",\"topModule\":\"cfg\",\"manager\":\"Tables\"}]}");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => new LubanConfigParser().Parse(
            new TableKitOptions
            {
                ProjectRoot = root,
                LubanConfigPath = config,
                AssemblyName = "../Outside"
            }));

        Assert.Contains("程序集名称", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>拒绝点分隔名称中的非法段，避免生成后才暴露无效 C# namespace 或类型名。</summary>
    [Fact]
    public void RejectsInvalidQualifiedIdentifierSegment()
    {
        string root = CreateTempDirectory();
        try
        {
            string config = Path.Combine(root, "luban.conf");
            File.WriteAllText(config, "{\"targets\":[{\"name\":\"client\",\"topModule\":\"Game.1Invalid\",\"manager\":\"Tables\"}]}");

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => new LubanConfigParser().Parse(
                new TableKitOptions
                {
                    ProjectRoot = root,
                    LubanConfigPath = config
                }));

            Assert.Contains("topModule", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>创建测试隔离目录。</summary>
    /// <returns>临时目录绝对路径。</returns>
    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "yokiframe-tablekit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
