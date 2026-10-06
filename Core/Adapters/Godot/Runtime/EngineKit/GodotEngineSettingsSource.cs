#if GODOT && TOOLS
using Godot;

namespace YokiFrame
{
    /// <summary>
    /// 读取 Godot 项目设置的最小访问器；抽出来是为了让开关判定在无 Godot 进程时也能单测。
    /// </summary>
    public interface IGodotProjectSettingsAccessor
    {
        /// <summary>判断键是否存在。</summary>
        /// <param name="key">Godot 项目设置键。</param>
        /// <returns>存在时返回 true。</returns>
        bool HasSetting(string key);

        /// <summary>读取布尔值。</summary>
        /// <param name="key">Godot 项目设置键。</param>
        /// <param name="value">读取结果。</param>
        /// <returns>键存在且为布尔时返回 true。</returns>
        bool TryReadBoolean(string key, out bool value);
    }

    /// <summary>基于 Godot <see cref="ProjectSettings"/> 的访问器。</summary>
    public sealed class GodotProjectSettingsAccessor : IGodotProjectSettingsAccessor
    {
        /// <summary>判断键是否存在。</summary>
        /// <param name="key">Godot 项目设置键。</param>
        /// <returns>存在时返回 true。</returns>
        public bool HasSetting(string key)
        {
            return ProjectSettings.HasSetting(key);
        }

        /// <summary>读取布尔值。</summary>
        /// <param name="key">Godot 项目设置键。</param>
        /// <param name="value">读取结果。</param>
        /// <returns>键存在且为布尔时返回 true。</returns>
        public bool TryReadBoolean(string key, out bool value)
        {
            value = false;
            if (!ProjectSettings.HasSetting(key))
            {
                return false;
            }

            Variant setting = ProjectSettings.GetSetting(key);
            if (setting.VariantType != Variant.Type.Bool)
            {
                return false;
            }

            value = setting.AsBool();
            return true;
        }
    }

    /// <summary>
    /// Godot 宿主的 Engine 执行开关：读 <c>project.godot</c> 的 <c>yokiframe/engine/operations_enabled</c>。
    /// </summary>
    /// <remarks>
    /// Godot 不使用 Unity 的 <c>editor-settings.json</c>：配置统一在 project.godot 的 yokiframe 前缀下。
    /// 缺失、类型不符或读取失败一律按关闭处理（fail-closed，§9 决议 B），并给出可诊断原因。
    /// </remarks>
    public sealed class GodotEngineSettingsSource : IYokiFrameEngineSettingsSource
    {
        /// <summary>执行开关所在的 Godot 项目设置键。</summary>
        public const string OPERATIONS_ENABLED_KEY = "yokiframe/engine/operations_enabled";

        private readonly IGodotProjectSettingsAccessor mAccessor;

        /// <summary>创建使用 Godot ProjectSettings 的开关读取器。</summary>
        public GodotEngineSettingsSource()
            : this(new GodotProjectSettingsAccessor())
        {
        }

        /// <summary>创建使用自定义访问器的开关读取器。</summary>
        /// <param name="accessor">项目设置访问器。</param>
        public GodotEngineSettingsSource(IGodotProjectSettingsAccessor accessor)
        {
            mAccessor = accessor;
        }

        /// <summary>读取开关；任何异常都转换为关闭快照。</summary>
        /// <returns>开关快照。</returns>
        public YokiFrameEngineSettingsSnapshot Read()
        {
            if (mAccessor == null)
            {
                return YokiFrameEngineSettingsSnapshot.InvalidConfig(
                    "Godot engine settings accessor is not available.");
            }

            try
            {
                if (!mAccessor.HasSetting(OPERATIONS_ENABLED_KEY))
                {
                    return YokiFrameEngineSettingsSnapshot.MissingConfig(
                        "Godot project settings has no '" + OPERATIONS_ENABLED_KEY + "' entry.");
                }

                if (!mAccessor.TryReadBoolean(OPERATIONS_ENABLED_KEY, out bool enabled))
                {
                    return YokiFrameEngineSettingsSnapshot.InvalidConfig(
                        "'" + OPERATIONS_ENABLED_KEY + "' must be a boolean in Godot project settings.");
                }

                return enabled
                    ? YokiFrameEngineSettingsSnapshot.Enabled()
                    : YokiFrameEngineSettingsSnapshot.Disabled(
                        "'" + OPERATIONS_ENABLED_KEY + "' is false in Godot project settings.");
            }
            catch (System.Exception exception)
            {
                return YokiFrameEngineSettingsSnapshot.InvalidConfig(
                    "Godot engine settings could not be read: " + exception.Message);
            }
        }
    }
}
#endif
