// <copyright project="NZCore.Editor" file="SettingsOverviewWindow.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace NZCore.Editor
{
    public class SettingsOverviewWindow : EditorWindow
    {
        private const string ScriptableObjectDatabaseTypeName = "NZCore.AssetManagement.ScriptableObjectDatabase`1";

        public static string SettingsRoot => NZCoreProjectSettings.instance.SettingsRoot;

        private readonly Dictionary<Object, Button> _assetButtons = new();
        private readonly Dictionary<string, Button> _typeButtons = new();
        private readonly List<SettingEntry> _settings = new();

        [SerializeField] private Object _selected;
        [SerializeField] private string _selectedTypeName;

        private ToolbarSearchField _search;
        private Label _countLabel;
        private ScrollView _navigation;
        private VisualElement _inspector;
        private VisualElement _typeInspector;
        private ToolbarButton _generateButton;
        private ToolbarButton _recompileButton;
        private ToolbarButton _deleteButton;
        private ObjectField _settingsRoot;

        [MenuItem("Tools/NZCore/Settings", false, 1)]
        public static void Open()
        {
            GetWindow<SettingsOverviewWindow>();
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider("Project/NZCore", SettingsScope.Project)
            {
                label = "NZCore",
                keywords = new HashSet<string> { "NZCore", "Settings", "Folder" },
                guiHandler = _ =>
                {
                    var current = AssetDatabase.LoadAssetAtPath<DefaultAsset>(SettingsRoot);
                    EditorGUI.BeginChangeCheck();
                    var selected = (DefaultAsset)EditorGUILayout.ObjectField("Settings Folder", current, typeof(DefaultAsset), false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        var path = AssetDatabase.GetAssetPath(selected);
                        if (IsValidSettingsRoot(path))
                        {
                            SetSettingsRoot(path);
                        }
                    }
                }
            };
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Settings", EditorGUIUtility.IconContent("Settings").image);
            minSize = new Vector2(900, 420);
            EditorApplication.projectChanged += Refresh;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= Refresh;
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            rootVisualElement.Add(CreateToolbar());

            var splitView = new TwoPaneSplitView(0, 280, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1;
            splitView.Add(CreateNavigation());

            _inspector = new VisualElement { style = { flexGrow = 1 } };
            splitView.Add(_inspector);
            rootVisualElement.Add(splitView);

            Refresh();
        }

        private VisualElement CreateToolbar()
        {
            var toolbar = new Toolbar();

            _search = new ToolbarSearchField { tooltip = "Filter settings" };
            _search.style.width = 240;
            _search.RegisterValueChangedCallback(_ => ApplySearch());
            toolbar.Add(_search);

            _settingsRoot = new ObjectField
            {
                objectType = typeof(DefaultAsset),
                allowSceneObjects = false,
                tooltip = "Settings folder"
            };
            _settingsRoot.style.width = 220;
            _settingsRoot.SetValueWithoutNotify(AssetDatabase.LoadAssetAtPath<DefaultAsset>(SettingsRoot));
            _settingsRoot.RegisterValueChangedCallback(change =>
            {
                var path = AssetDatabase.GetAssetPath(change.newValue);
                if (!IsValidSettingsRoot(path))
                {
                    _settingsRoot.SetValueWithoutNotify(change.previousValue);
                    return;
                }

                SetSettingsRoot(path);
            });
            toolbar.Add(_settingsRoot);
            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });

            toolbar.Add(CreateToolbarButton(Refresh, null, "Refresh", "Refresh settings"));
            _generateButton = CreateToolbarButton(GenerateAll, "Generate All", "d_TextAsset Icon", "Update every generated JSON setting");
            toolbar.Add(_generateButton);
            _recompileButton = CreateToolbarButton(Recompile, "Recompile", "d_Assembly Icon", "Request script compilation");
            toolbar.Add(_recompileButton);

            return toolbar;
        }

        private VisualElement CreateNavigation()
        {
            var root = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    backgroundColor = new Color(0.15f, 0.15f, 0.15f)
                }
            };

            var header = new VisualElement
            {
                style =
                {
                    height = 32,
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 8,
                    paddingRight = 8
                }
            };
            header.Add(new Label("Settings")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 }
            });
            _countLabel = new Label();
            header.Add(_countLabel);
            root.Add(header);

            _navigation = new ScrollView { style = { flexGrow = 1 } };
            root.Add(_navigation);
            return root;
        }

        private void Refresh()
        {
            if (_navigation == null)
            {
                return;
            }

            _settingsRoot?.SetValueWithoutNotify(AssetDatabase.LoadAssetAtPath<DefaultAsset>(SettingsRoot));
            _settings.Clear();
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var type in TypeCache.GetTypesDerivedFrom<ScriptableObject>()
                                          .Where(type => !type.IsAbstract && IsScriptableObjectDatabase(type)))
            {
                var filter = type.Namespace == null ? type.Name : $"{type.Namespace}.{type.Name}";
                foreach (var guid in AssetDatabase.FindAssets($"t:{filter}"))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath(path, type);

                    if (asset != null && asset.GetType() == type)
                    {
                        paths.Add(path);
                        _settings.Add(new SettingEntry(asset, path, true));
                    }
                }
            }

            var settingsRoot = NZCoreProjectSettings.instance.SettingsRoot;
            if (AssetDatabase.IsValidFolder(settingsRoot))
            {
                foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { settingsRoot }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) || !paths.Add(path))
                    {
                        continue;
                    }

                    var asset = AssetDatabase.LoadMainAssetAtPath(path);
                    if (asset != null)
                    {
                        _settings.Add(new SettingEntry(asset, path, settingsRoot));
                    }
                }
            }

            _settings.Sort((left, right) => string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase));
            NormalizeSelection();
            RefreshNavigation();
            ShowSelected();
            UpdateActionState();
        }

        private static bool IsValidSettingsRoot(string path) =>
            AssetDatabase.IsValidFolder(path)
            && (path == "Assets" || path.StartsWith("Assets/", StringComparison.Ordinal));

        private static void SetSettingsRoot(string path)
        {
            NZCoreProjectSettings.instance.SetSettingsRoot(path);
            foreach (var window in Resources.FindObjectsOfTypeAll<SettingsOverviewWindow>())
            {
                window.Refresh();
            }
        }

        private void NormalizeSelection()
        {
            if (!string.IsNullOrEmpty(_selectedTypeName))
            {
                var typeSettings = GetTypeSettings(_selectedTypeName).ToList();
                if (typeSettings.Count == 0)
                {
                    _selectedTypeName = null;
                }
                else if (typeSettings.All(setting => setting.Asset != _selected))
                {
                    _selected = typeSettings[0].Asset;
                }
            }

            if (string.IsNullOrEmpty(_selectedTypeName))
            {
                var generalSettings = _settings.Where(setting => setting.IsGeneral).ToList();
                if (_selected == null || generalSettings.All(setting => setting.Asset != _selected))
                {
                    _selected = generalSettings.FirstOrDefault()?.Asset;
                }
            }
        }

        private void ApplySearch()
        {
            var search = _search?.value?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(search))
            {
                NormalizeSelection();
            }
            else
            {
                var firstMatch = _settings.FirstOrDefault(setting => MatchesSearch(setting, search));
                _selected = firstMatch?.Asset;
                _selectedTypeName = firstMatch != null && !firstMatch.IsGeneral ? firstMatch.TypeName : null;
            }

            RefreshNavigation();
            ShowSelected();
        }

        private void RefreshNavigation()
        {
            if (_navigation == null)
            {
                return;
            }

            _navigation.Clear();
            _assetButtons.Clear();
            _typeButtons.Clear();

            var search = _search?.value?.Trim() ?? string.Empty;
            var generalSettings = _settings.Where(setting => setting.IsGeneral && MatchesSearch(setting, search)).ToList();
            if (generalSettings.Count > 0)
            {
                _navigation.Add(CreateSectionLabel("General"));
                foreach (var setting in generalSettings)
                {
                    _navigation.Add(CreateAssetButton(setting, () => SelectGeneral(setting.Asset)));
                }
            }

            var types = _settings.Where(setting => !setting.IsGeneral)
                                 .GroupBy(setting => setting.TypeName)
                                 .Where(group => TypeMatchesSearch(group, search))
                                 .OrderBy(group => GetTypeDisplayName(group.First().Asset.GetType()),
                                     StringComparer.OrdinalIgnoreCase)
                                 .ToList();
            if (types.Count > 0)
            {
                _navigation.Add(CreateSectionLabel("Asset Types"));
            }

            foreach (var type in types)
            {
                _navigation.Add(CreateTypeButton(type.Key, type.First().Asset.GetType(), type.Count()));
            }

            _countLabel.text = _settings.Count.ToString();
            UpdateSelectionStyles();
        }

        private Button CreateTypeButton(string typeName, Type type, int count)
        {
            var button = new ToolbarButton(() => SelectType(typeName));
            StyleListButton(button);
            button.tooltip = $"{type.FullName} ({count} asset{(count == 1 ? string.Empty : "s")})";
            button.Add(new Image
            {
                image = EditorGUIUtility.ObjectContent(null, type).image,
                scaleMode = ScaleMode.ScaleToFit,
                style = { width = 16, height = 16, marginRight = 6, flexShrink = 0 }
            });

            button.Add(new Label(GetTypeDisplayName(type))
            {
                style =
                {
                    flexGrow = 1,
                    whiteSpace = WhiteSpace.NoWrap,
                    textOverflow = TextOverflow.Ellipsis,
                    overflow = Overflow.Hidden
                }
            });
            button.Add(new Label(count.ToString())
            {
                style = { minWidth = 24, opacity = 0.65f, unityTextAlign = TextAnchor.MiddleRight }
            });

            _typeButtons.Add(typeName, button);
            return button;
        }

        private Button CreateAssetButton(SettingEntry setting, Action select)
        {
            var button = new ToolbarButton(select);
            StyleListButton(button);
            button.Add(new Image
            {
                image = EditorGUIUtility.ObjectContent(setting.Asset, setting.Asset.GetType()).image,
                scaleMode = ScaleMode.ScaleToFit,
                style = { width = 16, height = 16, marginRight = 6, flexShrink = 0 }
            });

            button.Add(new Label(ObjectNames.NicifyVariableName(setting.Asset.name))
            {
                style =
                {
                    flexGrow = 1,
                    whiteSpace = WhiteSpace.NoWrap,
                    textOverflow = TextOverflow.Ellipsis,
                    overflow = Overflow.Hidden
                }
            });
            if (!setting.IsGeneral)
            {
                button.Add(new Label(setting.Folder)
                {
                    style =
                    {
                        maxWidth = 90,
                        fontSize = 10,
                        opacity = 0.55f,
                        whiteSpace = WhiteSpace.NoWrap,
                        textOverflow = TextOverflow.Ellipsis,
                        overflow = Overflow.Hidden
                    }
                });
            }

            _assetButtons[setting.Asset] = button;
            return button;
        }

        private static Label CreateSectionLabel(string text) =>
            new(text)
            {
                style =
                {
                    height = 24,
                    paddingLeft = 8,
                    paddingTop = 5,
                    fontSize = 10,
                    opacity = 0.65f,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };

        private static void StyleListButton(Button button)
        {
            button.style.height = 28;
            button.style.minHeight = 28;
            button.style.marginTop = 0;
            button.style.marginBottom = 0;
            button.style.paddingLeft = 8;
            button.style.paddingRight = 6;
            button.style.flexDirection = FlexDirection.Row;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.FlexStart;
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.unityFontStyleAndWeight = FontStyle.Normal;
        }

        private void SelectGeneral(Object asset)
        {
            _selectedTypeName = null;
            _selected = asset;
            UpdateSelectionStyles();
            ShowSelected();
        }

        private void SelectType(string typeName)
        {
            _selectedTypeName = typeName;
            if (GetTypeSettings(typeName).All(setting => setting.Asset != _selected))
            {
                _selected = GetTypeSettings(typeName).FirstOrDefault()?.Asset;
            }

            UpdateSelectionStyles();
            ShowSelected();
        }

        private void SelectTypeAsset(Object asset)
        {
            _selected = asset;
            UpdateSelectionStyles();
            ShowAssetInspector(_typeInspector, _selected);
            _deleteButton?.SetEnabled(_selected != null);
        }

        private void ShowSelected()
        {
            if (_inspector == null)
            {
                return;
            }

            _inspector.Clear();
            _typeInspector = null;
            _deleteButton = null;
            foreach (var setting in _settings.Where(setting => !setting.IsGeneral))
            {
                _assetButtons.Remove(setting.Asset);
            }

            if (string.IsNullOrEmpty(_selectedTypeName))
            {
                ShowAssetInspector(_inspector, _selected);
                return;
            }

            ShowType();
        }

        private void ShowType()
        {
            var allSettings = GetTypeSettings(_selectedTypeName).ToList();
            var visibleSettings = FilterTypeSettings(allSettings).ToList();
            if (visibleSettings.Count > 0 && visibleSettings.All(setting => setting.Asset != _selected))
            {
                _selected = visibleSettings[0].Asset;
            }

            var type = allSettings[0].Asset.GetType();
            var header = CreateHeader(EditorGUIUtility.ObjectContent(null, type).image, GetTypeDisplayName(type), type.FullName);
            header.Add(CreateToolbarButton(CreateSelectedType, "Create", "Toolbar Plus", "Create setting asset"));
            _deleteButton = CreateToolbarButton(DeleteSelected, "Delete", "TreeEditor.Trash", "Move selected asset to trash");
            _deleteButton.SetEnabled(_selected != null);
            header.Add(_deleteButton);
            _inspector.Add(header);

            if (allSettings.Count == 1)
            {
                _selected = allSettings[0].Asset;
                _typeInspector = new VisualElement { style = { flexGrow = 1 } };
                _inspector.Add(_typeInspector);
                ShowAssetInspector(_typeInspector, _selected, false);
                UpdateSelectionStyles();
                return;
            }

            var splitView = new TwoPaneSplitView(0, 240, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1;

            var list = new ScrollView { style = { flexGrow = 1, paddingLeft = 4, paddingRight = 4 } };
            foreach (var setting in visibleSettings)
            {
                list.Add(CreateAssetButton(setting, () => SelectTypeAsset(setting.Asset)));
            }

            splitView.Add(list);
            _typeInspector = new VisualElement { style = { flexGrow = 1 } };
            splitView.Add(_typeInspector);
            _inspector.Add(splitView);

            ShowAssetInspector(_typeInspector, _selected);
            UpdateSelectionStyles();
        }

        private static void ShowAssetInspector(VisualElement parent, Object asset, bool showHeader = true)
        {
            if (parent == null)
            {
                return;
            }

            parent.Clear();
            if (asset == null)
            {
                return;
            }

            var path = AssetDatabase.GetAssetPath(asset);
            if (showHeader)
            {
                parent.Add(CreateHeader(EditorGUIUtility.ObjectContent(asset, asset.GetType()).image, asset.name, path, () => EditorGUIUtility.PingObject(asset)));
            }

            var scrollView = new ScrollView { style = { flexGrow = 1 } };
            var inspectorElement = new InspectorElement(asset);
            inspectorElement.style.paddingLeft = 8;
            inspectorElement.style.paddingRight = 8;
            inspectorElement.style.paddingBottom = 8;
            scrollView.Add(inspectorElement);
            parent.Add(scrollView);
        }

        private static VisualElement CreateHeader(Texture icon, string title, string subtitle, Action ping = null)
        {
            var header = new VisualElement
            {
                style =
                {
                    minHeight = 52,
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 10,
                    paddingRight = 6,
                    borderBottomWidth = 1,
                    borderBottomColor = new Color(0.08f, 0.08f, 0.08f)
                }
            };
            header.Add(new Image
            {
                image = icon,
                scaleMode = ScaleMode.ScaleToFit,
                style = { width = 32, height = 32, marginRight = 8, flexShrink = 0 }
            });

            var labels = new VisualElement { style = { flexGrow = 1, overflow = Overflow.Hidden } };
            labels.Add(new Label(title)
            {
                style = { fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold }
            });
            labels.Add(new Label(subtitle)
            {
                tooltip = subtitle,
                style =
                {
                    fontSize = 10,
                    opacity = 0.65f,
                    whiteSpace = WhiteSpace.NoWrap,
                    textOverflow = TextOverflow.Ellipsis,
                    overflow = Overflow.Hidden
                }
            });
            header.Add(labels);

            if (ping != null)
            {
                header.Add(CreateToolbarButton(ping, null, "d_Project", "Ping asset"));
            }

            return header;
        }

        private void CreateSelectedType()
        {
            var settings = GetTypeSettings(_selectedTypeName).ToList();
            var prototype = settings.FirstOrDefault(setting => setting.Asset == _selected) ?? settings.FirstOrDefault();
            if (prototype != null)
            {
                CreateSetting(prototype);
            }
        }

        private void CreateSetting(SettingEntry prototype)
        {
            var type = prototype.Asset.GetType();
            var directory = System.IO.Path.GetDirectoryName(prototype.Path)?.Replace('\\', '/');
            var fileName = $"New {ObjectNames.NicifyVariableName(type.Name)}.asset";
            var path = AssetDatabase.GenerateUniqueAssetPath($"{directory}/{fileName}");
            var asset = CreateInstance(type);

            AssetDatabase.CreateAsset(asset, path);
            Undo.RegisterCreatedObjectUndo(asset, "Create setting asset");
            AssetDatabase.SaveAssets();

            _selected = asset;
            _search?.SetValueWithoutNotify(string.Empty);
            Refresh();
            EditorGUIUtility.PingObject(asset);
        }

        private void DeleteSelected()
        {
            if (_selected == null || string.IsNullOrEmpty(_selectedTypeName))
            {
                return;
            }

            var path = AssetDatabase.GetAssetPath(_selected);
            if (!EditorUtility.DisplayDialog("Delete setting?", $"Move '{_selected.name}' to the trash?", "Delete", "Cancel"))
            {
                return;
            }

            _typeInspector?.Clear();
            if (AssetDatabase.MoveAssetToTrash(path))
            {
                _selected = null;
                Refresh();
            }
        }

        private void UpdateSelectionStyles()
        {
            foreach (var entry in _assetButtons)
            {
                SetSelected(entry.Value, entry.Key == _selected);
            }

            foreach (var entry in _typeButtons)
            {
                SetSelected(entry.Value, entry.Key == _selectedTypeName);
            }
        }

        private static void SetSelected(Button button, bool selected)
        {
            button.style.borderLeftWidth = selected ? 3 : 0;
            button.style.borderLeftColor = selected ? new Color(0.2f, 0.55f, 0.9f) : Color.clear;
            if (selected)
            {
                button.style.backgroundColor = new Color(0.16f, 0.34f, 0.52f);
            }
            else
            {
                button.style.backgroundColor = StyleKeyword.Null;
            }
        }

        private IEnumerable<SettingEntry> GetTypeSettings(string typeName)
        {
            return _settings.Where(setting => !setting.IsGeneral && setting.TypeName == typeName);
        }

        private IEnumerable<SettingEntry> FilterTypeSettings(IEnumerable<SettingEntry> settings)
        {
            var search = _search?.value?.Trim() ?? string.Empty;
            return string.IsNullOrEmpty(search)
                   || GetTypeDisplayName(_selected.GetType()).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                ? settings
                : settings.Where(setting => MatchesSearch(setting, search));
        }

        private void GenerateAll()
        {
            AssetDatabase.SaveAssets();
            ChangeProcessorEditorElement.Click_CodeGenAll();
            UpdateActionState();
        }

        private void Recompile()
        {
            AssetDatabase.SaveAssets();
            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.None);
            UpdateActionState();
        }

        private void OnCompilationStarted(object context)
        {
            UpdateActionState();
        }

        private void OnCompilationFinished(object context)
        {
            UpdateActionState();
        }

        private void UpdateActionState()
        {
            var enabled = !EditorApplication.isCompiling;
            _generateButton?.SetEnabled(enabled);
            _recompileButton?.SetEnabled(enabled);
        }

        private static bool TypeMatchesSearch(IEnumerable<SettingEntry> settings, string search)
        {
            var list = settings as IList<SettingEntry> ?? settings.ToList();
            return string.IsNullOrEmpty(search)
                   || GetTypeDisplayName(list[0].Asset.GetType()).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                   || list.Any(setting => MatchesSearch(setting, search));
        }

        private static bool MatchesSearch(SettingEntry setting, string search) =>
            string.IsNullOrEmpty(search)
            || setting.Asset.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
            || GetTypeDisplayName(setting.Asset.GetType()).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
            || setting.Path.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string GetTypeDisplayName(Type type)
        {
            var name = type.Name.StartsWith("Schema_", StringComparison.Ordinal) ? type.Name.Substring(7) : type.Name;
            if (name.EndsWith("Authoring", StringComparison.Ordinal))
            {
                name = name.Substring(0, name.Length - 9);
            }
            else if (name.EndsWith("Asset", StringComparison.Ordinal))
            {
                name = name.Substring(0, name.Length - 5);
            }

            return ObjectNames.NicifyVariableName(name);
        }

        private static bool IsScriptableObjectDatabase(Type type)
        {
            for (var baseType = type.BaseType; baseType != null; baseType = baseType.BaseType)
            {
                if (baseType.IsGenericType &&
                    baseType.GetGenericTypeDefinition().FullName == ScriptableObjectDatabaseTypeName)
                {
                    return true;
                }
            }

            return false;
        }

        private static ToolbarButton CreateToolbarButton(Action action, string text, string iconName, string tooltip)
        {
            var button = new ToolbarButton(action) { tooltip = tooltip };
            button.style.flexDirection = FlexDirection.Row;
            button.style.alignItems = Align.Center;

            var iconTexture = EditorGUIUtility.IconContent(iconName).image;
            if (iconTexture != null)
            {
                button.Add(new Image
                {
                    image = iconTexture,
                    scaleMode = ScaleMode.ScaleToFit,
                    style = { width = 16, height = 16, marginRight = text == null ? 0 : 3 }
                });
            }

            if (text != null)
            {
                button.Add(new Label(text));
            }

            return button;
        }

        private sealed class SettingEntry
        {
            public readonly Object Asset;
            public readonly string Path;
            public readonly string TypeName;
            public readonly string Folder;
            public readonly bool IsGeneral;

            public SettingEntry(Object asset, string path, bool isGeneral)
            {
                Asset = asset;
                Path = path;
                TypeName = asset.GetType().AssemblyQualifiedName;
                IsGeneral = isGeneral;
                Folder = string.Empty;
            }

            public SettingEntry(Object asset, string path, string settingsRoot)
            {
                Asset = asset;
                Path = path;
                TypeName = asset.GetType().AssemblyQualifiedName;

                var directory = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
                IsGeneral = directory == settingsRoot;
                Folder = IsGeneral
                    ? string.Empty
                    : string.Join(" / ", directory?.Substring(settingsRoot.Length + 1)
                                                       .Split('/')
                                                       .Select(ObjectNames.NicifyVariableName));
            }
        }
    }
}
