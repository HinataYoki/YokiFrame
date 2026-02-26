#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// BuffKit 实战：角色 Buff 系统文档
    /// </summary>
    internal static class BuffKitDocCharacter
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "实战：角色 Buff 系统",
                Description = "将 BuffKit 集成到角色系统中。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "角色类持有 BuffContainer",
                        Code = @"public class Character : IDisposable
{
    private readonly CharacterData mData;
    private readonly BuffContainer mBuffContainer;
    
    public Character(CharacterData data)
    {
        mData = data;
        mBuffContainer = BuffKit.CreateContainer();
    }
    
    // 计算最终属性
    public float Attack => mBuffContainer.GetModifiedValue(
        AttributeId.Attack, mData.BaseAttack);
    
    // 添加 Buff
    public bool AddBuff(int buffId)
    {
        return mBuffContainer.Add(buffId);
    }
    
    public bool AddBuff(IBuff buff)
    {
        return mBuffContainer.Add(buff);
    }
    
    // 每帧更新
    public void Update(float deltaTime)
    {
        mBuffContainer.Update(deltaTime);
    }
    
    // 释放资源
    public void Dispose()
    {
        mBuffContainer.Dispose();
    }
}",
                        Explanation = "角色持有容器，在 Update 中更新 Buff 时间。"
                    },
                    new()
                    {
                        Title = "使用示例",
                        Code = @"// 创建角色
var player = new Character(playerData);

// 给敌人上毒
enemy.AddBuff(new PoisonBuff(10f));

// 使用增益技能
player.AddBuff(new AttackUpBuff(0.3f));

// 添加控制免疫
player.AddImmunity(BuffTag.Control);

// 游戏循环
void Update()
{
    player.Update(Time.deltaTime);
    enemy.Update(Time.deltaTime);
}

// 销毁时释放
player.Dispose();",
                        Explanation = "BuffKit 通过 BuffContainer 与角色关联。"
                    }
                }
            };
        }
    }
}
#endif
