#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Architecture 文档模块的数据入口。
    /// </summary>
    internal static class ArchitectureDocData
    {
        /// <summary>
        /// 返回 Architecture 模块的全部文档章节。
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                ArchitectureDocOverview.CreateSection(),
                ArchitectureDocEditor.CreateSection(),
                ArchitectureDocIndex.CreateSection(),
                ArchitectureDocCreate.CreateSection(),
                ArchitectureDocService.CreateSection(),
                ArchitectureDocModel.CreateSection(),
                ArchitectureDocUsage.CreateSection(),
                ArchitectureDocSaveKit.CreateSection()
            };
        }
    }

    /// <summary>
    /// Architecture 模块中的动态框架索引章节。
    /// </summary>
    internal static class ArchitectureDocIndex
    {
        internal static DocSection CreateSection()
        {
            var section = new DocSection
            {
                Title = "框架索引",
                Description = "该章节会根据当前已注册的工具页面、文档模块与编辑器通信通道动态生成，反映 Core 当前实际发现到的编辑器侧结构。"
            };

            section.CodeExamples.Add(CreateSummaryExample());

            foreach (var overview in BuildKitOverviews())
            {
                section.CodeExamples.Add(overview);
            }

            return section;
        }

        private static CodeExample CreateSummaryExample()
        {
            int pageCount = YokiToolPageRegistry.PageInfos.Count;
            int moduleCount = DocumentationModuleRegistry.Modules.Count;
            int channelCount = EditorChannelRegistry.Channels.Count;

            return new CodeExample
            {
                Title = "注册表快照",
                Code =
                    $"工具页面数：{pageCount}\n" +
                    $"文档模块数：{moduleCount}\n" +
                    $"编辑器通道数：{channelCount}",
                Explanation = "这些统计值来自共享注册中心，代表当前已经被索引到的编辑器结构。"
            };
        }

        private static IEnumerable<CodeExample> BuildKitOverviews()
        {
            var pageCounts = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            var docCounts = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            var channelCounts = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            var pageNames = new Dictionary<string, List<string>>(System.StringComparer.OrdinalIgnoreCase);
            var docNames = new Dictionary<string, List<string>>(System.StringComparer.OrdinalIgnoreCase);
            var channelNames = new Dictionary<string, List<string>>(System.StringComparer.OrdinalIgnoreCase);
            var orderedKits = new List<string>();

            foreach (var page in YokiToolPageRegistry.PageInfos)
            {
                var kit = string.IsNullOrEmpty(page.Kit) ? "未知" : page.Kit;
                if (!pageCounts.ContainsKey(kit))
                {
                    orderedKits.Add(kit);
                    pageCounts[kit] = 0;
                    pageNames[kit] = new List<string>();
                }

                pageCounts[kit]++;
                pageNames[kit].Add($"{page.Name} [{page.Category}]");
            }

            foreach (var module in DocumentationModuleRegistry.Modules)
            {
                var kit = string.IsNullOrEmpty(module.Name) ? "未知" : module.Name;
                if (!docCounts.ContainsKey(kit))
                {
                    if (!orderedKits.Contains(kit))
                    {
                        orderedKits.Add(kit);
                    }

                    docCounts[kit] = 0;
                    docNames[kit] = new List<string>();
                }

                docCounts[kit]++;
                docNames[kit].Add(module.Name);
            }

            foreach (var channel in EditorChannelRegistry.Channels)
            {
                var kit = string.IsNullOrEmpty(channel.Kit) ? "未知" : channel.Kit;
                if (!channelCounts.ContainsKey(kit))
                {
                    if (!orderedKits.Contains(kit))
                    {
                        orderedKits.Add(kit);
                    }

                    channelCounts[kit] = 0;
                    channelNames[kit] = new List<string>();
                }

                channelCounts[kit]++;
                channelNames[kit].Add(string.IsNullOrEmpty(channel.DisplayName)
                    ? channel.Channel
                    : $"{channel.DisplayName} ({channel.Channel})");
            }

            orderedKits.Sort(System.StringComparer.OrdinalIgnoreCase);

            foreach (var kit in orderedKits)
            {
                pageCounts.TryGetValue(kit, out var pages);
                docCounts.TryGetValue(kit, out var modules);
                channelCounts.TryGetValue(kit, out var channels);
                pageNames.TryGetValue(kit, out var pagesList);
                docNames.TryGetValue(kit, out var modulesList);
                channelNames.TryGetValue(kit, out var channelsList);

                var builder = new StringBuilder(768);
                builder.Append("页面数：").Append(pages).AppendLine();
                builder.Append("文档模块数：").Append(modules).AppendLine();
                builder.Append("编辑器通道数：").Append(channels).AppendLine();
                AppendList(builder, "页面条目", pagesList);
                AppendList(builder, "文档条目", modulesList);
                AppendList(builder, "通道条目", channelsList);

                yield return new CodeExample
                {
                    Title = kit,
                    Code = builder.ToString(),
                    Explanation = BuildExplanation(kit, pages, modules, channels)
                };
            }
        }

        private static void AppendList(StringBuilder builder, string label, List<string> items)
        {
            builder.AppendLine().Append(label).Append('：');

            if (items == null || items.Count == 0)
            {
                builder.AppendLine().Append("  - 无");
                return;
            }

            items.Sort(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < items.Count; i++)
            {
                builder.AppendLine().Append("  - ").Append(items[i]);
            }
        }

        private static string BuildExplanation(string kit, int pages, int modules, int channels)
        {
            if (kit == "Documentation")
            {
                return "Documentation 是共享文档入口页，它负责汇总已注册模块，而不是一个可拆卸的运行时 Kit。";
            }

            if (kit == "Architecture")
            {
                return "Architecture 属于 Core 模块，负责定义框架级生命周期与服务组合规则。";
            }

            if (pages == 0 && channels > 0)
            {
                return "该 Kit 当前已提供编辑器通信元数据，但尚未在共享工具窗口中暴露独立页面。";
            }

            if (channels == 0 && pages > 0)
            {
                return "该 Kit 当前已暴露编辑器页面，但尚未注册共享编辑器通道元数据。";
            }

            return "该快照用于总结当前 Kit 已被注册的编辑器侧结构。";
        }
    }

    /// <summary>
    /// Architecture 文档模块提供器。
    /// </summary>
    internal sealed class ArchitectureDocumentationProvider : IDocumentationModuleProvider
    {
        public IEnumerable<DocModule> GetModules()
        {
            yield return new DocModule
            {
                Name = "架构",
                Icon = KitIcons.ARCHITECTURE,
                Category = "CORE",
                Description = "YokiFrame 核心架构系统，负责服务注册、依赖注入、编辑器与运行时边界以及模块化组织。",
                Keywords = new List<string> { "架构", "DI", "IoC", "服务", "模块", "编辑器" },
                Sections = ArchitectureDocData.GetAllSections()
            };
        }
    }
}
#endif
