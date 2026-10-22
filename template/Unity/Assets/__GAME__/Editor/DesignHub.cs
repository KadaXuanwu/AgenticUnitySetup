using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using __GAME__.Runtime;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace __GAME__.Editor {
    // One window for every asset a designer tunes, by topic: each page gathers GameConfig sections by their [Header],
    // whole assets under Content/Data by type, and the balance sheet sections that add them up. Nothing is lost when a
    // feature lands before its page does: an unplaced header or sheet section goes to Unsorted, an unplaced asset under
    // Other. Technical numbers live in Content/Tech and never show here.
    [NoAutoStaticsCleanup]
    public sealed class DesignHub : EditorWindow {
        private const string DataFolder = "Assets/__GAME__/Content/Data";
        private const string Layout = "Assets/__GAME__/Editor/DesignHub.uxml";
        private const string SelectedClass = "nav-item--selected";

        private sealed class Page {
            public string Group;
            public string Name;
            public string[] Headers = Array.Empty<string>();
            public string[] Types = Array.Empty<string>();
            public string[] Sheet = Array.Empty<string>();
            public string Path;
        }

        // The sidebar, in order. A new GameConfig header, sheet section or config type goes on a page here, e.g.
        // new Page { Group = "Balance", Name = "Economy", Headers = new[] { "Economy" }, Sheet = new[] { "Economy" } },
        // new Page { Group = "Feel", Name = "Effects", Types = new[] { "EffectsConfig" } },
        private static readonly Page[] Pages = {
        };

        // What the numbers add up to, by sheet section: each line a label and its value, worked out from the config.
        private static readonly Dictionary<string, Func<GameConfig, (string Label, string Value)[]>> SheetSections =
            new Dictionary<string, Func<GameConfig, (string Label, string Value)[]>> {
            };

        [SerializeField] private string selectedPage;

        private readonly List<Page> _pages = new List<Page>();
        private ILookup<string, string> _pathsByType;
        private ScrollView _nav;
        private ScrollView _content;
        private Label _title;
        private Label _path;

        [MenuItem("__GAME__/Design Hub")]
        public static void Open() {
            DesignHub hub = GetWindow<DesignHub>();
            hub.titleContent = new GUIContent("Design Hub");
            hub.minSize = new Vector2(760f, 420f);
        }

        public void CreateGUI() {
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Layout).CloneTree(rootVisualElement);
            _nav = rootVisualElement.Q<ScrollView>("nav");
            _content = rootVisualElement.Q<ScrollView>("content");
            _content.contentContainer.AddToClassList("cards");
            _title = rootVisualElement.Q<Label>("title");
            _path = rootVisualElement.Q<Label>("path");
            rootVisualElement.Q<Button>("ping").clicked += () =>
                EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(_pages.FirstOrDefault(page => page.Name == selectedPage)?.Path));
            ShowPlaying(EditorApplication.isPlayingOrWillChangePlaymode);
            BuildNav();
        }

        private void OnEnable() {
            EditorApplication.projectChanged += BuildNav;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable() {
            EditorApplication.projectChanged -= BuildNav;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange change) {
            ShowPlaying(change == PlayModeStateChange.EnteredPlayMode || change == PlayModeStateChange.ExitingEditMode);
        }

        private void ShowPlaying(bool playing) {
            rootVisualElement.Q(className: "hub")?.EnableInClassList("hub--playing", playing);
        }

        // Rebuilds only the list, so saving an asset never pulls the page out from under a field being edited.
        private void BuildNav() {
            if (_nav == null) {
                return;
            }
            CollectPages();
            _nav.Clear();
            string group = null;
            foreach (Page page in _pages) {
                if (page.Group != group) {
                    group = page.Group;
                    _nav.Add(new Label(group.ToUpperInvariant()) { pickingMode = PickingMode.Ignore }.WithClass("nav-group"));
                }
                Label item = new Label(page.Name) { userData = page.Name }.WithClass("nav-item");
                item.RegisterCallback<ClickEvent>(_ => Show(page));
                _nav.Add(item);
            }

            Page selected = _pages.FirstOrDefault(page => page.Name == selectedPage);
            if (_pages.Count == 0) {
                _content.Clear();
                _content.Add(new Label("No tunable numbers yet.").WithClass("card-hint"));
                _title.text = "Design Hub";
                _path.text = DataFolder;
            } else if (selected == null) {
                Show(_pages[0]);
            } else if (_content.childCount == 0) {
                Show(selected);
            } else {
                MarkSelected();
            }
        }

        private void CollectPages() {
            _pathsByType = AssetDatabase.FindAssets("t:ScriptableObject", new[] { DataFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path)
                .ToLookup(path => AssetDatabase.GetMainAssetTypeAtPath(path)?.Name);
            _pages.Clear();
            _pages.AddRange(Pages);

            string[] unsortedHeaders = typeof(GameConfig).GetFields()
                .Select(field => field.GetCustomAttribute<HeaderAttribute>()?.header)
                .Where(header => header != null && !Pages.Any(page => page.Headers.Contains(header)))
                .Distinct()
                .ToArray();
            string[] unsortedSheet = SheetSections.Keys.Where(section => !Pages.Any(page => page.Sheet.Contains(section))).ToArray();
            if (unsortedHeaders.Length > 0 || unsortedSheet.Length > 0) {
                _pages.Add(new Page { Group = "Other", Name = "Unsorted", Headers = unsortedHeaders, Sheet = unsortedSheet });
            }

            HashSet<string> placed = new HashSet<string>(Pages.SelectMany(page => page.Types)) { nameof(GameConfig) };
            foreach (IGrouping<string, string> type in _pathsByType.Where(type => !placed.Contains(type.Key))) {
                _pages.AddRange(type.Select(path => new Page { Group = "Other", Name = DisplayName(path), Types = new[] { type.Key } }));
            }
        }

        private void Show(Page page) {
            selectedPage = page.Name;
            MarkSelected();
            _content.Clear();
            GameConfig config = Load<GameConfig>();
            List<string> paths = new List<string>();
            if (config != null && page.Headers.Length > 0) {
                _content.Add(InspectorCard(config, null, page.Headers));
                paths.Add(AssetDatabase.GetAssetPath(config));
            }
            foreach (string path in page.Types.SelectMany(type => _pathsByType[type])) {
                ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                _content.Add(InspectorCard(asset, page.Types.Length > 1 ? DisplayName(path) : null, null));
                paths.Add(path);
            }
            if (config != null && page.Sheet.Length > 0) {
                _content.Add(SheetCard(config, page.Sheet));
            }
            page.Path = paths.Count == 0 ? DataFolder : paths.Aggregate(CommonPath);
            _title.text = page.Name;
            _path.text = page.Path;
        }

        private void MarkSelected() {
            _nav.Query<Label>(className: "nav-item")
                .ForEach(item => item.EnableInClassList(SelectedClass, (string)item.userData == selectedPage));
        }

        // Every visible field; given headers, only the fields under those.
        private static VisualElement InspectorCard(ScriptableObject asset, string title, string[] headers) {
            VisualElement card = new VisualElement().WithClass("card").WithClass("card--inspector");
            if (title != null) {
                card.Add(new Label(title).WithClass("card-title"));
            }
            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty field = serialized.GetIterator();
            field.NextVisible(true);
            string header = null;
            while (field.NextVisible(false)) {
                header = asset.GetType().GetField(field.name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.GetCustomAttribute<HeaderAttribute>()?.header ?? header;
                if (headers == null || headers.Contains(header)) {
                    card.Add(new PropertyField(field.Copy()));
                }
            }
            card.Bind(serialized);
            return card;
        }

        private static VisualElement SheetCard(GameConfig config, string[] sections) {
            VisualElement card = new VisualElement().WithClass("card").WithClass("card--sheet");
            card.Add(new Label("Sheet").WithClass("card-title"));
            VisualElement rows = new VisualElement();
            card.Add(rows);
            void Fill() {
                rows.Clear();
                foreach (string section in sections.Where(SheetSections.ContainsKey)) {
                    rows.Add(new Label(section.ToUpperInvariant()).WithClass("sheet-section"));
                    foreach ((string label, string value) in SheetSections[section](config)) {
                        VisualElement row = new VisualElement().WithClass("sheet-row");
                        row.Add(new Label(label).WithClass("sheet-label"));
                        row.Add(new Label(value).WithClass("sheet-value"));
                        rows.Add(row);
                    }
                }
            }
            Fill();
            // The sheet follows every edit on the cards beside it.
            card.TrackSerializedObjectValue(new SerializedObject(config), _ => Fill());
            return card;
        }

        private static T Load<T>() where T : ScriptableObject {
            string guid = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { DataFolder }).FirstOrDefault();
            return guid == null ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
        }

        // The deepest folder holding both, or the path itself when they are one asset.
        private static string CommonPath(string a, string b) {
            while (b != a && !b.StartsWith(a + "/")) {
                a = a.Substring(0, a.LastIndexOf('/'));
            }
            return a;
        }

        // "EffectsConfig" reads "Effects": every entry is a config, so the word says nothing.
        private static string DisplayName(string path) {
            string file = path.Substring(path.LastIndexOf('/') + 1);
            string name = ObjectNames.NicifyVariableName(file.Substring(0, file.LastIndexOf('.')));
            return name.EndsWith(" Config") ? name.Substring(0, name.Length - " Config".Length) : name;
        }
    }

    internal static class ElementExtensions {
        public static T WithClass<T>(this T element, string className) where T : VisualElement {
            element.AddToClassList(className);
            return element;
        }
    }
}
