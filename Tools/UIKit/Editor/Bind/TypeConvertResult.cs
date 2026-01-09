using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 类型转换操作结果
    /// </summary>
    public class TypeConvertResult
    {
        #region 状态

        /// <summary>
        /// 转换是否成功
        /// </summary>
        public bool Success;

        /// <summary>
        /// 错误消息（失败时）
        /// </summary>
        public string ErrorMessage;

        /// <summary>
        /// 源绑定类型
        /// </summary>
        public BindType SourceType;

        /// <summary>
        /// 目标绑定类型
        /// </summary>
        public BindType TargetType;

        #endregion

        #region 文件变更

        /// <summary>
        /// 需要创建的文件列表
        /// </summary>
        public List<string> FilesToCreate;

        /// <summary>
        /// 需要删除的文件列表
        /// </summary>
        public List<string> FilesToDelete;

        /// <summary>
        /// 需要修改的文件列表
        /// </summary>
        public List<string> FilesToModify;

        /// <summary>
        /// 所有受影响的文件列表（创建+删除+修改）
        /// </summary>
        public List<string> AffectedFiles;

        #endregion

        #region 冲突检测

        /// <summary>
        /// 是否存在命名冲突
        /// </summary>
        public bool HasNameConflict;

        /// <summary>
        /// 冲突的文件路径
        /// </summary>
        public string ConflictFilePath;

        /// <summary>
        /// 是否存在同名 Element（Element → Component 转换时）
        /// </summary>
        public bool HasSameNameElement;

        /// <summary>
        /// 使用同名 Element 的面板列表
        /// </summary>
        public List<string> PanelsUsingSameElement;

        #endregion

        #region 构造函数

        public TypeConvertResult()
        {
            FilesToCreate = new List<string>(4);
            FilesToDelete = new List<string>(4);
            FilesToModify = new List<string>(8);
            AffectedFiles = new List<string>(16);
            PanelsUsingSameElement = new List<string>(4);
        }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static TypeConvertResult Successful(BindType sourceType, BindType targetType)
        {
            return new TypeConvertResult
            {
                Success = true,
                SourceType = sourceType,
                TargetType = targetType
            };
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static TypeConvertResult Failed(string errorMessage, BindType sourceType, BindType targetType)
        {
            return new TypeConvertResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                SourceType = sourceType,
                TargetType = targetType
            };
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 添加需要创建的文件
        /// </summary>
        public void AddFileToCreate(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            FilesToCreate.Add(filePath);
            AffectedFiles.Add(filePath);
        }

        /// <summary>
        /// 添加需要删除的文件
        /// </summary>
        public void AddFileToDelete(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            FilesToDelete.Add(filePath);
            AffectedFiles.Add(filePath);
        }

        /// <summary>
        /// 添加需要修改的文件
        /// </summary>
        public void AddFileToModify(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            FilesToModify.Add(filePath);
            AffectedFiles.Add(filePath);
        }

        /// <summary>
        /// 是否有任何文件变更
        /// </summary>
        public bool HasAnyFileChanges
            => FilesToCreate.Count > 0 || FilesToDelete.Count > 0 || FilesToModify.Count > 0;

        /// <summary>
        /// 总受影响文件数
        /// </summary>
        public int TotalAffectedFiles => AffectedFiles.Count;

        /// <summary>
        /// 是否可以执行转换（无冲突且无错误）
        /// </summary>
        public bool CanExecute
            => !HasNameConflict && string.IsNullOrEmpty(ErrorMessage);

        /// <summary>
        /// 获取转换描述
        /// </summary>
        public string GetConversionDescription()
            => $"{SourceType} → {TargetType}";

        /// <summary>
        /// 获取结果摘要
        /// </summary>
        public string GetSummary()
        {
            if (!Success)
                return $"转换失败: {ErrorMessage}";

            return $"转换成功: 创建 {FilesToCreate.Count} 个文件, " +
                   $"修改 {FilesToModify.Count} 个文件, " +
                   $"删除 {FilesToDelete.Count} 个文件";
        }

        #endregion
    }
}
