using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 批量绑定操作结果
    /// </summary>
    public class BatchBindResult
    {
        #region 统计数据

        /// <summary>
        /// 总处理数量
        /// </summary>
        public int TotalProcessed;

        /// <summary>
        /// 成功添加绑定的数量
        /// </summary>
        public int SuccessCount;

        /// <summary>
        /// 跳过的数量（已有 Bind 组件）
        /// </summary>
        public int SkippedCount;

        /// <summary>
        /// 失败的数量
        /// </summary>
        public int FailedCount;

        #endregion

        #region 详细信息

        /// <summary>
        /// 跳过的对象路径列表
        /// </summary>
        public List<string> SkippedObjects;

        /// <summary>
        /// 失败的对象路径列表
        /// </summary>
        public List<string> FailedObjects;

        /// <summary>
        /// 失败原因列表（与 FailedObjects 一一对应）
        /// </summary>
        public List<string> FailureReasons;

        /// <summary>
        /// 成功添加的 Bind 组件列表
        /// </summary>
        public List<AbstractBind> AddedBinds;

        #endregion

        #region 构造函数

        public BatchBindResult()
        {
            SkippedObjects = new List<string>(8);
            FailedObjects = new List<string>(4);
            FailureReasons = new List<string>(4);
            AddedBinds = new List<AbstractBind>(16);
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 记录成功添加的绑定
        /// </summary>
        /// <param name="bind">添加的 Bind 组件</param>
        public void RecordSuccess(AbstractBind bind)
        {
            SuccessCount++;
            TotalProcessed++;
            if (bind != null)
                AddedBinds.Add(bind);
        }

        /// <summary>
        /// 记录跳过的对象
        /// </summary>
        /// <param name="objectPath">对象路径</param>
        public void RecordSkipped(string objectPath)
        {
            SkippedCount++;
            TotalProcessed++;
            SkippedObjects.Add(objectPath ?? string.Empty);
        }

        /// <summary>
        /// 记录失败的对象
        /// </summary>
        /// <param name="objectPath">对象路径</param>
        /// <param name="reason">失败原因</param>
        public void RecordFailed(string objectPath, string reason)
        {
            FailedCount++;
            TotalProcessed++;
            FailedObjects.Add(objectPath ?? string.Empty);
            FailureReasons.Add(reason ?? string.Empty);
        }

        /// <summary>
        /// 操作是否完全成功（无失败）
        /// </summary>
        public bool IsFullySuccessful => FailedCount == 0;

        /// <summary>
        /// 操作是否有任何成功
        /// </summary>
        public bool HasAnySuccess => SuccessCount > 0;

        /// <summary>
        /// 获取结果摘要
        /// </summary>
        public string GetSummary()
        {
            return $"处理完成: 成功 {SuccessCount}, 跳过 {SkippedCount}, 失败 {FailedCount}, 共 {TotalProcessed}";
        }

        #endregion
    }
}
