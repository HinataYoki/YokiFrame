#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    public partial class DocumentationToolPage
    {
        private DocModule CreateActionKitDoc()
        {
            return new DocModule
            {
                Name = "ActionKit",
                Icon = KitIcons.ACTIONKIT,
                Category = "TOOLS",
                Description = "动作序列工具，提供延时、回调、插值、序列、并行等动作的组合执行。支持对象池复用，避免 GC。",
                Keywords = new List<string> { "动作序列", "延时", "插值", "链式" },
                Sections = new List<DocSection>
                {
                    new()
                    {
                        Title = "基础动作",
                        Description = "ActionKit 提供多种基础动作类型。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "延时动作",
                                Code = @"// 延时执行
ActionKit.Delay(2f, () => Debug.Log(""2秒后执行""))
    .Start(this);

// 延时帧数
ActionKit.DelayFrame(10, () => Debug.Log(""10帧后执行""))
    .Start(this);

// 下一帧执行
ActionKit.NextFrame(() => Debug.Log(""下一帧执行""))
    .Start(this);",
                                Explanation = "Start 方法需要传入 MonoBehaviour 作为执行载体。"
                            },
                            new()
                            {
                                Title = "回调与插值",
                                Code = @"// 立即执行回调
ActionKit.Callback(() => Debug.Log(""立即执行""))
    .Start(this);

// 数值插值
ActionKit.Lerp(0f, 100f, 1f, 
    value => slider.value = value,
    () => Debug.Log(""插值完成""))
    .Start(this);",
                                Explanation = "Lerp 动作在指定时间内从 a 插值到 b，每帧调用 onLerp 回调。"
                            }
                        }
                    },
                    new()
                    {
                        Title = "序列与并行",
                        Description = "组合多个动作按顺序或同时执行。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "序列执行",
                                Code = @"// 按顺序执行多个动作
ActionKit.Sequence()
    .Append(ActionKit.Delay(1f, () => Debug.Log(""第1秒"")))
    .Append(ActionKit.Delay(1f, () => Debug.Log(""第2秒"")))
    .Append(ActionKit.Callback(() => Debug.Log(""完成"")))
    .Start(this);

// 嵌套序列
ActionKit.Sequence()
    .Append(ActionKit.Delay(1f, null))
    .Sequence(s => s
        .Append(ActionKit.Callback(() => Debug.Log(""嵌套1"")))
        .Append(ActionKit.Callback(() => Debug.Log(""嵌套2""))))
    .Start(this);"
                            },
                            new()
                            {
                                Title = "并行执行",
                                Code = @"// 同时执行多个动作
ActionKit.Parallel()
    .Append(ActionKit.Delay(1f, () => Debug.Log(""动作A完成"")))
    .Append(ActionKit.Delay(2f, () => Debug.Log(""动作B完成"")))
    .Start(this, controller =>
    {
        Debug.Log(""所有动作完成"");
    });

// 不等待全部完成（任一完成即结束）
ActionKit.Parallel(waitAll: false)
    .Append(ActionKit.Delay(1f, null))
    .Append(ActionKit.Delay(2f, null))
    .Start(this);"
                            }
                        }
                    },
                    new()
                    {
                        Title = "重复与条件",
                        Description = "循环执行动作或根据条件控制执行。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "重复执行",
                                Code = @"// 重复指定次数
ActionKit.Repeat(3)
    .Append(ActionKit.Delay(1f, () => Debug.Log(""重复执行"")))
    .Start(this);

// 无限重复
ActionKit.Repeat(-1)
    .Append(ActionKit.Delay(0.5f, () => Debug.Log(""每0.5秒执行"")))
    .Start(this);

// 条件重复
int count = 0;
ActionKit.Repeat(condition: () => count < 5)
    .Append(ActionKit.Callback(() => count++))
    .Append(ActionKit.Delay(0.5f, null))
    .Start(this);"
                            }
                        }
                    },
                    new()
                    {
                        Title = "异步动作",
                        Description = "支持协程、Task 和 UniTask 的异步动作。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "协程动作",
                                Code = @"// 包装协程
ActionKit.Coroutine(() => LoadResourceCoroutine())
    .Start(this);

IEnumerator LoadResourceCoroutine()
{
    yield return new WaitForSeconds(1f);
    Debug.Log(""资源加载完成"");
}"
                            },
                            new()
                            {
                                Title = "UniTask 动作",
                                Code = @"// 包装 UniTask
ActionKit.UniTask(() => LoadResourceAsync())
    .Start(this);

// 支持取消
ActionKit.UniTask(async ct =>
{
    await UniTask.Delay(1000, cancellationToken: ct);
    Debug.Log(""完成"");
}).Start(this);

// UniTask 延时（推荐）
ActionKit.DelayUniTask(2f, () => Debug.Log(""2秒后""))
    .Start(this);

// 等待条件
ActionKit.WaitUntil(() => isReady, () => Debug.Log(""条件满足""))
    .Start(this);",
                                Explanation = "UniTask 版本性能更好，推荐在支持 UniTask 的项目中使用。"
                            }
                        }
                    },
                    new()
                    {
                        Title = "控制器",
                        Description = "通过控制器管理动作的暂停、恢复和停止。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "动作控制",
                                Code = @"// 获取控制器
var controller = ActionKit.Sequence()
    .Append(ActionKit.Delay(5f, null))
    .Start(this);

// 暂停
controller.Pause();

// 恢复
controller.Resume();

// 停止（会触发回收）
controller.Stop();

// 完成回调
ActionKit.Delay(1f, null)
    .Start(this, ctrl => Debug.Log(""动作完成""));"
                            }
                        }
                    }
                }
            };
        }
    }
}
#endif
