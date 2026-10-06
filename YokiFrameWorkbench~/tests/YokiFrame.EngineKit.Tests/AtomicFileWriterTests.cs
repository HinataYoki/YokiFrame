using System;
using System.IO;
using System.Text;
using Xunit;

namespace YokiFrame.EngineKit.Tests;

/// <summary>
/// 原子写在**真实调用点**上的行为：eval 记录与运行记录都必须「覆盖成功、临时文件不残留、UTF-8 无 BOM」。
/// 共享实现 YokiFrameAtomicFileWriter 是 internal，因此从公开存储入口验证。
/// </summary>
public sealed class AtomicFileWriterTests : IDisposable
{
    private readonly string mRoot = Path.Combine(
        Path.GetTempPath(),
        "yokiframe-atomic-writer-tests",
        Guid.NewGuid().ToString("N"));

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(mRoot))
        {
            Directory.Delete(mRoot, recursive: true);
        }
    }

    [Fact]
    public void Eval_store_save_overwrites_without_temp_leftovers()
    {
        var store = new YokiFrameEngineEvalStore(mRoot);
        YokiFrameEngineEvalRecord record = CreateEvalRecord("eval-1", "first-content");

        store.Save(record);
        record.Note = "second";
        store.Save(record);

        Assert.True(store.TryRead("eval-1", out YokiFrameEngineEvalRecord reloaded));
        Assert.Equal("second", reloaded.Note);
        AssertNoTempLeftovers(Path.Combine(mRoot, ".yokiframe", "engine", "eval"));
    }

    [Fact]
    public void Run_store_save_overwrites_without_temp_leftovers()
    {
        var store = new YokiFrameEngineRunStore(mRoot);
        var record = new YokiFrameEngineRunRecord
        {
            RunId = "run-1",
            LegacyEntry = "run.pass",
            State = YokiFrameRunStatus.Queued,
            SubmittedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        store.Save(record);
        record.Note = "updated";
        store.Save(record);

        Assert.True(store.TryReadRun("run-1", out YokiFrameEngineRunRecord reloaded));
        Assert.Equal("updated", reloaded.Note);
        AssertNoTempLeftovers(Path.Combine(mRoot, ".yokiframe", "engine", "runs"));
    }

    [Fact]
    public void Written_records_are_utf8_without_bom()
    {
        var store = new YokiFrameEngineEvalStore(mRoot);
        store.Save(CreateEvalRecord("eval-2", "会话"));

        string path = Path.Combine(mRoot, ".yokiframe", "engine", "eval", "eval-2.json");
        byte[] bytes = File.ReadAllBytes(path);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        Assert.Contains("会话", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
    }

    private static YokiFrameEngineEvalRecord CreateEvalRecord(string id, string note)
    {
        return new YokiFrameEngineEvalRecord
        {
            Id = id,
            Token = "token",
            State = YokiFrameEngineEvalStates.READY,
            Note = note,
            SubmittedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private static void AssertNoTempLeftovers(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        string[] files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
        for (var index = 0; index < files.Length; index++)
        {
            Assert.DoesNotContain(".tmp", Path.GetFileName(files[index]), StringComparison.Ordinal);
        }
    }
}
