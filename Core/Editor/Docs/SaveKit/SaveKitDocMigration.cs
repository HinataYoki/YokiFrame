#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SaveKit 版本迁移文档
    /// </summary>
    internal static class SaveKitDocMigration
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "版本迁移",
                Description = "处理存档数据结构变更，支持链式迁移。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "注册迁移器",
                        Code = @"// 设置当前版本
SaveKit.SetCurrentVersion(3);

// 注册迁移器（v1→v2→v3 链式迁移）
SaveKit.RegisterMigrator(new V1ToV2Migrator());
SaveKit.RegisterMigrator(new V2ToV3Migrator());

// 实现迁移器
public class V1ToV2Migrator : ISaveMigrator
{
    public int FromVersion => 1;
    public int ToVersion => 2;

    public SaveData Migrate(SaveData data)
    {
        // 获取旧数据
        var oldPlayer = data.GetModule<OldPlayerData>();
        
        // 转换为新格式
        var newPlayer = new PlayerData
        {
            Name = oldPlayer.Name,
            Level = oldPlayer.Lv,
            Exp = 0
        };
        
        // 注册新数据
        data.RegisterModule(newPlayer);
        return data;
    }
}",
                        Explanation = "加载旧版本存档时自动按顺序执行迁移器，迁移后自动保存。"
                    }
                }
            };
        }
    }
}
#endif
