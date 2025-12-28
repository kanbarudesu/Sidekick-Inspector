using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System;

namespace Kanbarudesu.SidekickInspector
{
    [Serializable]
    public struct SerializedComponent
    {
        public string TypeName;
        public string JsonData;
    }

    [Serializable]
    internal class ClipboardWrapper
    {
        public List<SerializedComponent> Items = new();
    }

    [InitializeOnLoad]
    public static class SidekickComponentClipboard
    {
        private const string SESSION_KEY = "Sidekick_ClipboardData";
        private static List<SerializedComponent> _serializedData = new();
        public static bool HasData => _serializedData.Count > 0;
        public static int Count => _serializedData.Count;

        static SidekickComponentClipboard()
        {
            LoadFromSession();
        }

        public static void Copy(IEnumerable<Component> components)
        {
            _serializedData.Clear();
            foreach (var component in components)
            {
                if (component == null || component is Transform) continue;

                _serializedData.Add(new SerializedComponent
                {
                    TypeName = component.GetType().AssemblyQualifiedName,
                    JsonData = EditorJsonUtility.ToJson(component)
                });
            }
            SaveToSession();
        }

        public static bool CanPaste(GameObject target)
        {
            return HasData && target != null;
        }

        public static void PasteAsNew(GameObject target)
        {
            if (!CanPaste(target)) return;

            Undo.RegisterCompleteObjectUndo(target, "Paste Components As New");
            foreach (var data in _serializedData)
            {
                Type type = Type.GetType(data.TypeName);
                if (type == null) continue;

                Component newComp = Undo.AddComponent(target, type);
                EditorJsonUtility.FromJsonOverwrite(data.JsonData, newComp);
            }
            EditorUtility.SetDirty(target);
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }

        public static void PasteValues(GameObject target)
        {
            if (!CanPaste(target)) return;

            var targetComponents = target.GetComponents<Component>();
            foreach (var data in _serializedData)
            {
                bool copied = false;
                Type type = Type.GetType(data.TypeName);
                foreach (var targetComponent in targetComponents)
                {
                    if (targetComponent.GetType() == type)
                    {
                        Undo.RecordObject(targetComponent, "Paste Component Values");
                        EditorJsonUtility.FromJsonOverwrite(data.JsonData, targetComponent);
                        copied = true;
                        break;
                    }
                }

                if (!copied)
                    Debug.LogWarning($"Could not find component {type.Name} on {target.name} to paste values to.");
            }
            EditorUtility.SetDirty(target);
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }

        public static void PasteAsNewToNewGameObject()
        {
            var go = new GameObject("New GameObject");
            Undo.RegisterCreatedObjectUndo(go, "Create New GameObject");
            PasteAsNew(go);
            Selection.activeGameObject = go;
        }

        private static void SaveToSession()
        {
            string json = JsonUtility.ToJson(new ClipboardWrapper { Items = _serializedData });
            SessionState.SetString(SESSION_KEY, json);
        }

        private static void LoadFromSession()
        {
            string json = SessionState.GetString(SESSION_KEY, "");
            if (!string.IsNullOrEmpty(json))
            {
                var wrapper = JsonUtility.FromJson<ClipboardWrapper>(json);
                _serializedData = wrapper.Items;
            }
        }

        public static void Clear()
        {
            _serializedData.Clear();
            SessionState.EraseString(SESSION_KEY);
        }
    }
}