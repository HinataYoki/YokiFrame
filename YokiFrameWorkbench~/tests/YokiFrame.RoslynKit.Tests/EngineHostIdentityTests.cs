using YokiFrame;

namespace YokiFrame.RoslynKit.Tests;

/// <summary>
/// 会话身份读取（engine.json）：读到即用、读不到如实报原因、按 mtime 缓存但会跟随更新。
/// </summary>
public sealed class EngineHostIdentityTests
{
    /// <summary>从已发布 registry 读出会话标识和代数。成功时原因为空，不改文件。</summary>
    [Fact]
    public void Reads_session_identity_from_published_registry()
    {
        using var fixture = new IdentityFixture();
        fixture.Write("{\"engineId\":\"unity-editor\",\"sessionId\":\"sess-1\",\"generation\":42}");

        var reader = new RoslynHostIdentityReader(fixture.RegistryPath);

        Assert.True(reader.TryRead(out string sessionId, out long generation, out string reason));
        Assert.Equal("sess-1", sessionId);
        Assert.Equal(42L, generation);
        Assert.Equal(string.Empty, reason);
    }

    /// <summary>generation 以字符串发布时仍解析为整数。不把字符串原样返回。</summary>
    [Fact]
    public void Accepts_generation_published_as_string()
    {
        using var fixture = new IdentityFixture();
        fixture.Write("{\"sessionId\":\"sess-2\",\"generation\":\"7\"}");

        var reader = new RoslynHostIdentityReader(fixture.RegistryPath);

        Assert.True(reader.TryRead(out _, out long generation, out _));
        Assert.Equal(7L, generation);
    }

    /// <summary>文件缺失、JSON 损坏或没有 sessionId 时失败，并给出原因。不伪造会话或代数。</summary>
    [Fact]
    public void Reports_reasons_without_faking_identity()
    {
        using var missing = new IdentityFixture(createFile: false);
        var missingReader = new RoslynHostIdentityReader(missing.RegistryPath);
        Assert.False(missingReader.TryRead(out string missingSession, out long missingGeneration, out string missingReason));
        Assert.Equal(string.Empty, missingSession);
        Assert.Equal(0L, missingGeneration);
        Assert.Contains("was not found", missingReason, StringComparison.Ordinal);

        using var malformed = new IdentityFixture();
        malformed.Write("{\"sessionId\":");
        Assert.False(new RoslynHostIdentityReader(malformed.RegistryPath).TryRead(out _, out _, out string malformedReason));
        Assert.Contains("not valid JSON", malformedReason, StringComparison.Ordinal);

        using var anonymous = new IdentityFixture();
        anonymous.Write("{\"engineId\":\"unity-editor\"}");
        Assert.False(new RoslynHostIdentityReader(anonymous.RegistryPath).TryRead(out _, out _, out string anonymousReason));
        Assert.Contains("no sessionId", anonymousReason, StringComparison.Ordinal);
    }

    /// <summary>缓存跟随 registry 内容更新；内容不变时重复读取保持稳定。不依赖 mtime 粒度。</summary>
    [Fact]
    public void Cache_follows_registry_updates()
    {
        using var fixture = new IdentityFixture();
        fixture.Write("{\"sessionId\":\"sess-a\",\"generation\":1}");
        var reader = new RoslynHostIdentityReader(fixture.RegistryPath);
        Assert.True(reader.TryRead(out string first, out long firstGeneration, out _));
        Assert.Equal("sess-a", first);
        Assert.Equal(1L, firstGeneration);

        // 内容长度不同 → 缓存必然失效（不依赖 mtime 粒度）。
        fixture.Write("{\"sessionId\":\"sess-bb\",\"generation\":2222}");

        Assert.True(reader.TryRead(out string second, out long secondGeneration, out _));
        Assert.Equal("sess-bb", second);
        Assert.Equal(2222L, secondGeneration);

        // 无变化时重复读取保持稳定
        Assert.True(reader.TryRead(out string third, out long thirdGeneration, out _));
        Assert.Equal("sess-bb", third);
        Assert.Equal(2222L, thirdGeneration);
    }

    private sealed class IdentityFixture : IDisposable
    {
        /// <summary>创建临时 registry 目录。需要文件时先写入空对象，避免读取落到其他夹具。</summary>
        /// <param name="createFile">为 false 时不创建 engine.json。</param>
        internal IdentityFixture(bool createFile = true)
        {
            Root = Path.Combine(Path.GetTempPath(), "yokiframe-identity-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            RegistryPath = Path.Combine(Root, "engine.json");
            if (createFile)
            {
                Write("{}");
            }
        }

        internal string Root { get; }

        internal string RegistryPath { get; }

        /// <summary>覆盖写入 registry 文本。不追加，不改路径。</summary>
        /// <param name="json">完整 JSON 文本，可以是故意损坏的片段。</param>
        internal void Write(string json)
        {
            File.WriteAllText(RegistryPath, json);
        }

        /// <summary>删除临时目录。删除遇到 IOException 时吞掉，避免掩盖用例本身的断言。</summary>
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch (IOException)
            {
            }
        }
    }
}
