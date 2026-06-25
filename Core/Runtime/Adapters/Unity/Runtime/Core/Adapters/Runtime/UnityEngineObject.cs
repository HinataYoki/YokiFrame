#if !GODOT
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>
    /// IEngineObject 的 Unity 实现，封装 UnityEngine.GameObject。
    /// </summary>
    public sealed class UnityEngineObject : IEngineObject
    {
        private GameObject mGameObject;

        /// <summary>
        /// 底层的 Unity GameObject（适配器层可直接访问）
        /// </summary>
        public GameObject GameObject
        {
            get => mGameObject;
            set => mGameObject = value;
        }

        /// <summary>
        /// Unity 对象名称。
        /// </summary>
        public string Name
        {
            get => mGameObject != null ? mGameObject.name : string.Empty;
            set { if (mGameObject != null) mGameObject.name = value; }
        }

        /// <summary>
        /// Unity GameObject 的激活状态。
        /// </summary>
        public bool IsActive
        {
            get => mGameObject != null && mGameObject.activeSelf;
            set { if (mGameObject != null) mGameObject.SetActive(value); }
        }

        /// <summary>
        /// Unity Transform 的世界坐标。
        /// </summary>
        public YokiVector3 Position
        {
            get
            {
                if (mGameObject == null)
                    return YokiVector3.Zero;

                return mGameObject.transform.position.ToYokiVector3();
            }
            set
            {
                if (mGameObject != null)
                    mGameObject.transform.position = value.ToUnityVector3();
            }
        }

        /// <summary>
        /// 创建空的 Unity 对象包装器。
        /// </summary>
        public UnityEngineObject() { }

        /// <summary>
        /// 使用指定 GameObject 创建 Unity 对象包装器。
        /// </summary>
        /// <param name="gameObject">要包装的 GameObject。</param>
        public UnityEngineObject(GameObject gameObject)
        {
            mGameObject = gameObject;
        }

        /// <summary>
        /// 从现有 GameObject 创建包装器。
        /// </summary>
        /// <param name="gameObject">要包装的 GameObject。</param>
        /// <returns>创建好的对象包装器；对象无效时返回 null。</returns>
        public static UnityEngineObject Wrap(GameObject gameObject)
        {
            if (gameObject == default)
                return null;

            return new(gameObject);
        }

        /// <summary>
        /// 获取指定类型的 Unity Component。
        /// </summary>
        /// <typeparam name="T">目标组件类型。</typeparam>
        /// <returns>找到的组件；对象无效或组件不存在时返回 null。</returns>
        public T GetComponent<T>() where T : class
        {
            if (mGameObject == default)
                return null;

            return mGameObject.GetComponent<T>();
        }

        /// <summary>
        /// 销毁此对象。
        /// </summary>
        public void Destroy()
        {
            if (mGameObject == default)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEngine.Object.DestroyImmediate(mGameObject);
            else
#endif
                UnityEngine.Object.Destroy(mGameObject);
            mGameObject = null;
        }

        /// <summary>
        /// 实例化预制体并返回包装器。
        /// </summary>
        /// <param name="prefab">要实例化的预制体包装器。</param>
        /// <returns>实例化后的对象包装器；预制体无效时返回 null。</returns>
        public IEngineObject Instantiate(IEngineObject prefab)
        {
            if (prefab is not UnityEngineObject unityPrefab || unityPrefab.mGameObject == default)
                return null;

            var instantiated = UnityEngine.Object.Instantiate(unityPrefab.mGameObject);
            return new UnityEngineObject(instantiated);
        }

        /// <summary>
        /// 隐式转换：UnityEngineObject -> GameObject。
        /// </summary>
        /// <param name="engineObject">YokiFrame Unity 对象包装器。</param>
        public static implicit operator GameObject(UnityEngineObject engineObject)
        {
            return engineObject != null ? engineObject.mGameObject : default;
        }

        /// <summary>
        /// 隐式转换：GameObject -> UnityEngineObject。
        /// </summary>
        /// <param name="gameObject">Unity GameObject。</param>
        public static implicit operator UnityEngineObject(GameObject gameObject)
            => gameObject != default ? new UnityEngineObject(gameObject) : null;
    }
}
#endif
