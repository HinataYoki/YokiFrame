using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YokiFrame
{
    [MonoSingletonPath("UIKit/UIRoot",true)]
    public class UIRoot : MonoBehaviour, ISingleton
    {
        private static UIRoot mInstance;
        public static UIRoot Instance
        {
            get
            {
                if (mInstance == null)
                {
                    mInstance = FindFirstObjectByType<UIRoot>();

                    if (mInstance == null)
                    {
                        var uikitPrefab = Resources.Load<GameObject>(nameof(UIKit));
                        var uikit = Instantiate(uikitPrefab);
                        uikit.name = nameof(UIKit);
                        DontDestroyOnLoad(uikit);
                        mInstance = uikit.GetComponentInChildren<UIRoot>();


                        UILevelDic.Clear();
                        foreach (UILevel level in Enum.GetValues(typeof(UILevel)))
                        {
                            var obj = new GameObject(level.ToString(),typeof(RectTransform));
                            UILevelDic.Add(level, obj.transform as RectTransform);
                            UILevelDic[level].SetParent(mInstance.transform);
                            if (UILevelDic[level] is RectTransform rect)
                            {
                                rect.anchorMin = Vector2.zero;
                                rect.anchorMax = Vector2.one;
                                rect.anchoredPosition3D = Vector3.zero;
                                rect.localEulerAngles = Vector3.zero;
                                rect.localScale = Vector3.one;

                                rect.sizeDelta = Vector2.zero;
                            }
                        }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                        // 只启用了新版 Input System
                        mInstance.EventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#elif ENABLE_LEGACY_INPUT_MANAGER && !ENABLE_INPUT_SYSTEM
                        // 只启用了旧版 Input Manager
                        mInstance.EventSystem.gameObject.AddComponent<StandaloneInputModule>();
#else
                        // 两个系统都启用了
                        mInstance.EventSystem.gameObject.AddComponent<StandaloneInputModule>();
#endif
                    }
                }

                return mInstance;
            }
        }

        public static Dictionary<UILevel, RectTransform> UILevelDic = new();

        public Canvas Canvas;
        public CanvasScaler CanvasScaler;
        public GraphicRaycaster GraphicRaycaster;
        public EventSystem EventSystem;

        private Camera mUICamera;
        public Camera UICamera
        {
            get => mUICamera;
            set
            {
                if (value != null)
                {
                    mUICamera = value;
                    mInstance.Canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    mInstance.Canvas.worldCamera = mUICamera;
                }
            }
        }

        public void SetLevelOfPanel(UILevel level, IPanel panel)
        {
            if (panel == null) return;

            var canvas = panel.Transform.GetComponent<Canvas>();

            if (canvas)
            {
                panel.Transform.SetParent(UILevelDic[UILevel.CanvasPanel]);
            }
            else
            {
                panel.Transform.SetParent(UILevelDic[level]);
            }
            SetDefaultSizeOfPanel(panel);
        }


        public void SetDefaultSizeOfPanel(IPanel panel)
        {
            var rect = panel.Transform as RectTransform;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition3D = Vector3.zero;
            rect.localEulerAngles = Vector3.zero;
            rect.localScale = Vector3.one;

            rect.sizeDelta = Vector2.zero;
        }

        void ISingleton.OnSingletonInit() { }
    }

}