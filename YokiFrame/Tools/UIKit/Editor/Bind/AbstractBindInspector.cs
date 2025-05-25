using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    [CustomEditor(typeof(AbstractBind), true)]
    [CanEditMultipleObjects]
    public class AbstractBindInspector : Editor
    {
        private AbstractBind Target => target as AbstractBind;

        private readonly Lazy<GUIStyle> Label12 = new(() => new GUIStyle(GUI.skin.label)
        {
            fontSize = 12
        });

        private string[] mComponentNames;
        private int mComponentNameIndex;

        private void OnEnable()
        {
            mComponentNames = Target.GetComponents<Component>()
                .Where(component => component != null && component is not AbstractBind)
                .Select(component => component.GetType().FullName)
                .ToArray();

            if (!string.IsNullOrEmpty(Target.TypeName))
            {
                mComponentNameIndex = Array.FindIndex(mComponentNames, c => c.Contains(Target.TypeName));
            }

            if (mComponentNameIndex <= 0 || mComponentNameIndex >= mComponentNames.Length)
            {
                mComponentNameIndex = mComponentNames.Length - 1;
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            GUILayout.BeginVertical("box");
            {
                #region BindType
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("绑定类型", Label12.Value, GUILayout.Width(100));

                    EditorGUI.BeginChangeCheck();
                    Target.customBind = (BindType)EditorGUILayout.EnumPopup(Target.Bind);
                    if (EditorGUI.EndChangeCheck())
                    {
                        EditorUtility.SetDirty(target);
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(10);
                #endregion

                #region Name
                //非叶子类型则可以自定义字段名称
                if (Target.Bind is not BindType.Leaf)
                {
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("字段名称", Label12.Value, GUILayout.Width(100));

                        if (string.IsNullOrEmpty(Target.customName))
                        {
                            Target.customName = Target.name;
                        }

                        EditorGUI.BeginChangeCheck();
                        Target.customName = EditorGUILayout.TextField(Target.customName);
                        if (EditorGUI.EndChangeCheck())
                        {
                            if (string.IsNullOrEmpty(Target.customName))
                            {
                                Target.customName = Target.name;
                            }
                            EditorUtility.SetDirty(target);
                        }
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.Space(10);
                }
                #endregion

                #region TypeName
                //属于元素或者组件类型则可以自定义类名
                if (Target.Bind is BindType.Element or BindType.Component)
                {
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("类名称", Label12.Value, GUILayout.Width(100));
                        if (string.IsNullOrEmpty(Target.customType))
                        {
                            Target.customType = Target.name;
                        }
                        Target.customType = EditorGUILayout.TextField(Target.customType, GUILayout.ExpandWidth(true));
                        Target.type = Target.customType;
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);
                }
                //如果是成员类型则可以选择组件列表
                else if (Target.Bind is BindType.Member)
                {
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("组件列表", Label12.Value, GUILayout.Width(100));
                        if (string.IsNullOrEmpty(Target.autoType))
                        {
                            Target.autoType = mComponentNames[mComponentNameIndex];
                        }
                        mComponentNameIndex = EditorGUILayout.Popup(mComponentNameIndex, mComponentNames);
                        Target.autoType = mComponentNames[mComponentNameIndex];
                        Target.type = Target.autoType;
                    }
                    GUILayout.EndHorizontal();
                }
                #endregion

                #region Comment
                //非叶子类型则可以添加注释
                if (Target.Bind is not BindType.Leaf)
                {
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("注释", Label12.Value, GUILayout.Width(100));
                        EditorGUI.BeginChangeCheck();
                        Target.customComment = EditorGUILayout.TextField(Target.Comment, GUILayout.ExpandWidth(true));
                        if (EditorGUI.EndChangeCheck())
                        {
                            EditorUtility.SetDirty(target);
                        }
                    }
                    GUILayout.EndHorizontal();
                }
                #endregion
            }
            GUILayout.EndVertical();
        }
    }
}