namespace YokiFrame.Packaging.Tests;

/// <summary>
/// 标记 Packaging 顺序执行集合，防止修改进程级 Console.Error 的测试并行污染其他测试。
/// </summary>
[CollectionDefinition("PackagingSequential")]
public sealed class PackagingSequentialCollection;
