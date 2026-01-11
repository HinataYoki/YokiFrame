#if YOKIFRAME_INPUTSYSTEM_SUPPORT
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 基于 PlayerPrefs 的输入绑定持久化实现
    /// </summary>
    public sealed class PlayerPrefsPersistence : IInputPersistence
    {
        public void Save(string key, string json)
        {
            if (string.IsNullOrEmpty(key)) return;
            
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();
        }

        public string Load(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            
            return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
        }

        public void Delete(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }

        public bool Exists(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            
            return PlayerPrefs.HasKey(key);
        }
    }
}

#endif