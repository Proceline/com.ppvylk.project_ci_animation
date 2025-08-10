using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using ProjectCI_Animation.Runtime.Interface;

namespace ProjectCI_Animation.Runtime.Editor
{
    [CustomEditor(typeof(AnimationPlayableSupportBase<>), true)]
    public class AnimationPlayableSupportBaseEditor : UnityEditor.Editor
    {
        private string[] _animationNames;
        private UnityEngine.Object _newClipInfo;

        private void OnEnable()
        {
            _animationNames = Enum.GetNames(typeof(AnimationIndexName));
        }

        protected virtual string GetNameByIndexInEditor(int index)
        {
            return $"{index}";
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var defaultInfosProp = serializedObject.FindProperty("defaultAnimationClipInfos");
            Type elementType = null;
            if (defaultInfosProp != null && defaultInfosProp.isArray)
            {
                // 获取元素类型
                var targetType = target.GetType();
                while (targetType != null && (!targetType.IsGenericType || targetType.GetGenericTypeDefinition() != typeof(AnimationPlayableSupportBase<>)))
                {
                    targetType = targetType.BaseType;
                }
                if (targetType != null)
                {
                    elementType = targetType.GetGenericArguments()[0];
                }

                EditorGUILayout.LabelField("Default Animation Clip Infos", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                HashSet<string> assignedNames = new HashSet<string>();
                for (int i = 0; i < defaultInfosProp.arraySize; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    string label = i < _animationNames.Length ? _animationNames[i] : GetNameByIndexInEditor(i);
                    var elementProp = defaultInfosProp.GetArrayElementAtIndex(i);
                    EditorGUILayout.PropertyField(elementProp, new GUIContent($"[{label}]"), true);
                    
                    if (GUILayout.Button("Delete", GUILayout.Width(60)))
                    {
                        defaultInfosProp.DeleteArrayElementAtIndex(i);
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                    
                    if (elementProp.objectReferenceValue is IAnimationClipInfo && i < _animationNames.Length)
                    {
                        assignedNames.Add(_animationNames[i]);
                    }
                }
                EditorGUI.indentLevel--;

                // 添加新clipInfo区域
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Add New ClipInfo", EditorStyles.boldLabel);
                if (elementType != null && typeof(ScriptableObject).IsAssignableFrom(elementType))
                {
                    _newClipInfo = EditorGUILayout.ObjectField(_newClipInfo, elementType, false);
                    EditorGUI.BeginDisabledGroup(_newClipInfo == null);
                    if (GUILayout.Button("Add ClipInfo"))
                    {
                        defaultInfosProp.InsertArrayElementAtIndex(defaultInfosProp.arraySize);
                        defaultInfosProp.GetArrayElementAtIndex(defaultInfosProp.arraySize - 1).objectReferenceValue = _newClipInfo;
                        _newClipInfo = null;
                    }
                    EditorGUI.EndDisabledGroup();
                }

                // 显示未分配动画名
                List<string> unassigned = new List<string>();
                for (int i = 0; i < _animationNames.Length; i++)
                {
                    if (!assignedNames.Contains(_animationNames[i]))
                        unassigned.Add(_animationNames[i]);
                }
                if (unassigned.Count > 0)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.HelpBox($"Unassigned Animation Names: {string.Join(", ", unassigned)}", MessageType.Info);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No defaultAnimationClipInfos array found.", MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
} 