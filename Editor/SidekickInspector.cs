using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace Kanbarudesu.SidekickInspector
{
    [InitializeOnLoad]
    public static class SidekickInspector
    {
        private const float HorizontalPadding = 4f;
        private const float InspectorPadding = 16f;

        private static readonly HashSet<int> _visibleComponentIds = new HashSet<int>();

        private static HashSet<Type> _cachedCommonTypes;
        private static int _lastSelectionHash;
        private static readonly Dictionary<Type, Component> _firstGoCompMap = new Dictionary<Type, Component>();

        private static GUIStyle _allButtonStyle;
        private static GUIStyle AllButtonStyle
        {
            get
            {
                if (_allButtonStyle == null)
                {
                    _allButtonStyle = new GUIStyle(EditorStyles.toolbarButton) { fixedWidth = 40f };
                }
                return _allButtonStyle;
            }
        }

        static SidekickInspector()
        {
            Editor.finishedDefaultHeaderGUI += OnFinishedHeaderGUI;
        }

        private static void OnFinishedHeaderGUI(Editor editor)
        {
            var targets = editor.targets;
            if (targets == null || targets.Length == 0 || !(targets[0] is GameObject))
                return;

            var commonTypes = GetCommonComponentTypesCached(targets);

            if (targets.Length > 1)
                HideNonCommonComponents(targets, commonTypes);

            var firstGo = targets[0] as GameObject;
            Component[] firstGoComponents = firstGo.GetComponents<Component>();

            _firstGoCompMap.Clear();
            foreach (var component in firstGoComponents)
            {
                if (component == null) continue;
                Type type = component.GetType();
                if (!_firstGoCompMap.ContainsKey(type)) _firstGoCompMap[type] = component;
            }

            SyncVisibleComponents(firstGoComponents, commonTypes);
            ComponentClipboardButton(targets);

            bool allVisible = _visibleComponentIds.Count == commonTypes.Count;
            float viewWidth = EditorGUIUtility.currentViewWidth - InspectorPadding;
            float currentRowWidth = 0f;
            int rowCount = 0;

            using (new EditorGUILayout.VerticalScope(EditorStyles.toolbar))
            {
                var allContent = EditorGUIUtility.IconContent(allVisible ? "d_scenevis_visible" : "d_scenevis_hidden");
                allContent.text = "All";
                DrawWrappedToggle(allContent, AllButtonStyle, ref currentRowWidth, ref rowCount, viewWidth, allVisible,
                    () => ShowAllComponentsOnTargets(targets, commonTypes));

                foreach (var type in commonTypes)
                {
                    if (type == null) continue;

                    bool isVisibleOnReference = IsTypeVisible(firstGo, type);
                    bool isSelected = isVisibleOnReference && !allVisible;

                    _firstGoCompMap.TryGetValue(type, out var refComponent);
                    GUIContent content = refComponent != null ? GetComponentContent(refComponent) : new GUIContent(type.Name);

                    DrawWrappedToggle(content, EditorStyles.toolbarButton, ref currentRowWidth, ref rowCount, viewWidth, isSelected,
                        () => OnComponentTypeButtonClicked(targets, type, commonTypes.Count));
                }

                if (currentRowWidth > 0f)
                    GUILayout.EndHorizontal();
            }

            GUILayout.Space(EditorGUIUtility.singleLineHeight * (rowCount - 1));
            GUILayout.Space(4);
        }

        private static HashSet<Type> GetCommonComponentTypesCached(UnityEngine.Object[] targets)
        {
            unchecked
            {
                int currentHash = 17;
                for (int i = 0; i < targets.Length; i++)
                {
                    var go = targets[i] as GameObject;
                    if (go == null) continue;

                    currentHash = currentHash * 31 + go.GetInstanceID();
                    currentHash = currentHash * 31 + go.GetComponents<Component>().Length;
                }

                if (_cachedCommonTypes != null && currentHash == _lastSelectionHash)
                    return _cachedCommonTypes;

                _lastSelectionHash = currentHash;
            }
            _cachedCommonTypes = GetCommonComponentTypes(targets);
            return _cachedCommonTypes;
        }

        private static void OnComponentTypeButtonClicked(UnityEngine.Object[] targets, Type type, int commonCount)
        {
            if (_visibleComponentIds.Count == commonCount)
            {
                ShowOnlyTypeOnTargets(targets, type);
                return;
            }

            bool currentlyVisible = IsTypeVisible(targets[0] as GameObject, type);
            foreach (var target in targets)
            {
                var go = target as GameObject;
                if (go == null) continue;

                var components = go.GetComponents(type);
                foreach (var component in components)
                {
                    HideFlags targetFlag = currentlyVisible ? (component.hideFlags | HideFlags.HideInInspector) : (component.hideFlags & ~HideFlags.HideInInspector);
                    if (component.hideFlags != targetFlag)
                    {
                        component.hideFlags = targetFlag;
                        EditorUtility.SetDirty(component);
                    }
                }
            }
            _lastSelectionHash = -1;
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }

        private static void ShowOnlyTypeOnTargets(UnityEngine.Object[] targets, Type targetType)
        {
            foreach (var target in targets)
            {
                var go = target as GameObject;
                if (go == null) continue;

                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;

                    HideFlags targetFlag = (component.GetType() == targetType)
                        ? (component.hideFlags & ~HideFlags.HideInInspector)
                        : (component.hideFlags | HideFlags.HideInInspector);

                    if (component.hideFlags != targetFlag)
                    {
                        component.hideFlags = targetFlag;
                        EditorUtility.SetDirty(component);
                    }
                }
            }
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }

        private static void HideNonCommonComponents(UnityEngine.Object[] targets, HashSet<Type> commonTypes)
        {
            foreach (var target in targets)
            {
                var go = target as GameObject;
                if (go == null) continue;

                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null || component is Transform) continue;

                    if (!commonTypes.Contains(component.GetType()))
                    {
                        if ((component.hideFlags & HideFlags.HideInInspector) == 0)
                        {
                            component.hideFlags |= HideFlags.HideInInspector;
                            EditorUtility.SetDirty(component);
                        }
                    }
                }
            }
        }

        private static void ShowAllComponentsOnTargets(UnityEngine.Object[] targets, HashSet<Type> commonTypes)
        {
            foreach (var target in targets)
            {
                var go = target as GameObject;
                if (go == null) continue;

                foreach (var c in go.GetComponents<Component>())
                {
                    if (c == null) continue;
                    if (commonTypes.Contains(c.GetType()) && (c.hideFlags & HideFlags.HideInInspector) != 0)
                    {
                        c.hideFlags &= ~HideFlags.HideInInspector;
                        EditorUtility.SetDirty(c);
                    }
                }
            }
            _lastSelectionHash = -1;
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }

        private static void SyncVisibleComponents(Component[] components, HashSet<Type> commonTypes)
        {
            _visibleComponentIds.Clear();
            foreach (var component in components)
            {
                if (component != null && commonTypes.Contains(component.GetType()))
                {
                    if ((component.hideFlags & HideFlags.HideInInspector) == 0)
                        _visibleComponentIds.Add(component.GetInstanceID());
                }
            }
        }

        private static HashSet<Type> GetCommonComponentTypes(UnityEngine.Object[] targets)
        {
            if (targets == null || targets.Length == 0) return new HashSet<Type>();
            var firstGo = targets[0] as GameObject;
            if (firstGo == null) return new HashSet<Type>();

            HashSet<Type> commonTypes = new HashSet<Type>();
            foreach (var comp in firstGo.GetComponents<Component>())
            {
                if (comp != null) commonTypes.Add(comp.GetType());
            }

            for (int i = 1; i < targets.Length; i++)
            {
                var go = targets[i] as GameObject;
                if (go == null) continue;
                var currentTypes = new HashSet<Type>();
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp != null) currentTypes.Add(comp.GetType());
                }
                commonTypes.IntersectWith(currentTypes);
            }
            return commonTypes;
        }

        private static bool IsTypeVisible(GameObject go, Type type)
        {
            var comp = go.GetComponent(type);
            if (comp == null) return false;
            return (comp.hideFlags & HideFlags.HideInInspector) == 0;
        }

        private static GUIContent GetComponentContent(Component component)
        {
            GUIContent content = EditorGUIUtility.ObjectContent(component, component.GetType());
            content.text = component.GetType().Name;
            return content;
        }

        private static void DrawWrappedToggle(GUIContent content, GUIStyle style, ref float currentRowWidth, ref int rowCount, float maxWidth, bool isOn, Action onValueChanged)
        {
            using (new IconSizeScope(new Vector2(14f, 14f)))
            {
                Vector2 size = style.CalcSize(content);
                float controlWidth = size.x + HorizontalPadding;

                if (currentRowWidth + controlWidth > maxWidth && currentRowWidth > 0)
                {
                    GUILayout.EndHorizontal();
                    currentRowWidth = 0f;
                }

                if (currentRowWidth == 0f)
                {
                    GUILayout.BeginHorizontal(EditorStyles.toolbar);
                    rowCount++;
                }

                bool newState = GUILayout.Toggle(isOn, content, style, GUILayout.ExpandWidth(false));
                if (newState != isOn) onValueChanged?.Invoke();

                currentRowWidth += controlWidth;
            }
        }

        private static void ComponentClipboardButton(UnityEngine.Object[] targets)
        {
            using (new GUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var copyContent = EditorGUIUtility.IconContent("TreeEditor.Duplicate");
                copyContent.text = "Copy Selection";
                if (GUILayout.Button(copyContent, EditorStyles.toolbarButton))
                    CopySelectedComponents(targets[0] as GameObject);

                int pasteCount = SidekickComponentClipboard.Count;
                var pasteContent = EditorGUIUtility.IconContent("Clipboard");
                pasteContent.text = (targets.Length > 1 ? "Paste to All" : "Paste Selection") + (pasteCount > 0 ? $" ({pasteCount})" : "");

                if (GUILayout.Button(pasteContent, EditorStyles.toolbarButton))
                {
                    var menu = new GenericMenu();
                    if (SidekickComponentClipboard.HasData)
                    {
                        menu.AddItem(new GUIContent("Paste As New"), false, () => { foreach (var t in targets) SidekickComponentClipboard.PasteAsNew(t as GameObject); });
                        menu.AddItem(new GUIContent("Paste Values"), false, () => { foreach (var t in targets) SidekickComponentClipboard.PasteValues(t as GameObject); });
                        menu.AddItem(new GUIContent("Paste As New on New GameObject"), false, SidekickComponentClipboard.PasteAsNewToNewGameObject);
                        menu.AddSeparator("");
                        menu.AddItem(new GUIContent("Clear Clipboard"), false, SidekickComponentClipboard.Clear);
                    }
                    else menu.AddDisabledItem(new GUIContent("No Data"));
                    menu.ShowAsContext();
                }
            }
        }

        private static void CopySelectedComponents(GameObject go)
        {
            List<Component> selected = new();
            foreach (var c in go.GetComponents<Component>())
            {
                if (c != null && _visibleComponentIds.Contains(c.GetInstanceID()))
                    selected.Add(c);
            }
            SidekickComponentClipboard.Copy(selected);
        }
    }

    public readonly struct IconSizeScope : IDisposable
    {
        private readonly Vector2 _prev;
        public IconSizeScope(Vector2 size) { _prev = EditorGUIUtility.GetIconSize(); EditorGUIUtility.SetIconSize(size); }
        public void Dispose() => EditorGUIUtility.SetIconSize(_prev);
    }
}