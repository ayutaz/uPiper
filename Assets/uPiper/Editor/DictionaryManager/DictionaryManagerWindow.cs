using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using uPiper.Core.Phonemizers;
using uPiper.Editor.Menu;
using UnityEditor;
using UnityEngine;

namespace uPiper.Editor.DictionaryManager
{
    /// <summary>
    /// IMGUI ベースの EditorWindow によるカスタム辞書管理 GUI。
    /// StreamingAssets/uPiper/Dictionaries/ 配下の辞書を閲覧・編集できる。
    /// user_custom_dict.json のみ編集・削除可能。プリセット辞書は閲覧専用。
    /// </summary>
    public sealed class DictionaryManagerWindow : EditorWindow
    {
        // --- Constants ---

        private const string UserCustomDictFileName = "user_custom_dict.json";
        private const string WindowTitle = "Dictionary Manager";
        private const float MinWindowWidth = 700f;
        private const float MinWindowHeight = 400f;
        private const double SearchDebounceSeconds = 0.2;

        private static readonly string[] PriorityFilterLabels =
        {
            "All", "Low (1-3)", "Medium (4-6)", "High (7-8)", "Critical (9-10)"
        };

        // --- Internal entry struct ---

        private struct EntryView
        {
            public string Word;
            public string Pronunciation;
            public int Priority;
            public string FileName;
            public bool IsEditable;
        }

        // --- State ---

        private List<EntryView> _allEntries = new();
        private List<EntryView> _filteredEntries = new();
        private Vector2 _entryListScroll;
        private Vector2 _previewScroll;

        // Search / filter
        private string _searchText = "";
        private string _pendingSearchText = "";
        private double _lastSearchInputTime;
        private int _fileFilterIndex;
        private int _priorityFilterIndex;
        private string[] _fileFilterLabels = { "All" };

        // Add / Edit panel
        private bool _showEditPanel;
        private bool _isEditing;
        private string _editWord = "";
        private string _editPronunciation = "";
        private int _editPriority = 5;
        private string _editOriginalWord = "";

        // Preview panel
        private bool _showPreviewPanel;
        private string _previewInputText = "";
        private string _previewResultText = "";
        private IReadOnlyList<CustomDictionary.ReplacementDetail> _previewReplacements =
            Array.Empty<CustomDictionary.ReplacementDetail>();

        // Concurrency guard
        private Dictionary<string, DateTime> _fileWriteTimes = new();

        // Styles (lazy init)
        private GUIStyle _headerStyle;
        private GUIStyle _toolbarButtonStyle;
        private bool _stylesInitialized;

        // --- Menu ---

        [MenuItem("uPiper/Dictionary Manager", false, uPiperMenuStructure.PRIORITY_TOOLS + 10)]
        private static void ShowWindow()
        {
            var window = GetWindow<DictionaryManagerWindow>(WindowTitle);
            window.minSize = new Vector2(MinWindowWidth, MinWindowHeight);
            window.Show();
        }

        // --- Lifecycle ---

        private void OnEnable()
        {
            ReloadAll();
        }

        private void OnFocus()
        {
            ReloadAll();
        }

        // --- GUI ---

        private void OnGUI()
        {
            InitStyles();
            DrawToolbar();
            DrawSearchBar();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DrawEntryList();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            if (_showEditPanel)
            {
                DrawEditPanel();
            }

            DrawPreviewPanel();

            // Debounced search
            if (_pendingSearchText != _searchText &&
                EditorApplication.timeSinceStartup - _lastSearchInputTime >= SearchDebounceSeconds)
            {
                _searchText = _pendingSearchText;
                ApplyFilters();
                Repaint();
            }
        }

        // --- Toolbar ---

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Import JSON", _toolbarButtonStyle, GUILayout.Width(90)))
            {
                ImportJson();
            }

            if (GUILayout.Button("Export", _toolbarButtonStyle, GUILayout.Width(60)))
            {
                ExportJson();
            }

            if (GUILayout.Button("Reload All", _toolbarButtonStyle, GUILayout.Width(80)))
            {
                ReloadAll();
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label($"Entries: {_allEntries.Count}", EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        // --- Search Bar ---

        private void DrawSearchBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUI.BeginChangeCheck();
            var newSearch = EditorGUILayout.TextField(_pendingSearchText, EditorStyles.toolbarSearchField,
                GUILayout.MinWidth(150));
            if (EditorGUI.EndChangeCheck())
            {
                _pendingSearchText = newSearch;
                _lastSearchInputTime = EditorApplication.timeSinceStartup;
            }

            EditorGUI.BeginChangeCheck();
            _fileFilterIndex = EditorGUILayout.Popup(_fileFilterIndex, _fileFilterLabels,
                EditorStyles.toolbarPopup, GUILayout.Width(180));
            if (EditorGUI.EndChangeCheck())
            {
                ApplyFilters();
            }

            EditorGUI.BeginChangeCheck();
            _priorityFilterIndex = EditorGUILayout.Popup(_priorityFilterIndex, PriorityFilterLabels,
                EditorStyles.toolbarPopup, GUILayout.Width(120));
            if (EditorGUI.EndChangeCheck())
            {
                ApplyFilters();
            }

            if (GUILayout.Button("+ Add Entry", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                BeginAddEntry();
            }

            EditorGUILayout.EndHorizontal();
        }

        // --- Entry List ---

        private void DrawEntryList()
        {
            // Column headers
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Word", EditorStyles.miniBoldLabel, GUILayout.Width(180));
            GUILayout.Label("Pronunciation", EditorStyles.miniBoldLabel, GUILayout.Width(180));
            GUILayout.Label("Priority", EditorStyles.miniBoldLabel, GUILayout.Width(70));
            GUILayout.Label("File", EditorStyles.miniBoldLabel, GUILayout.Width(160));
            GUILayout.Label("Actions", EditorStyles.miniBoldLabel, GUILayout.Width(80));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            _entryListScroll = EditorGUILayout.BeginScrollView(_entryListScroll, GUILayout.ExpandHeight(true));

            for (var i = 0; i < _filteredEntries.Count; i++)
            {
                var entry = _filteredEntries[i];
                var bgColor = i % 2 == 0 ? new Color(0f, 0f, 0f, 0.05f) : Color.clear;
                var rect = EditorGUILayout.BeginHorizontal();
                EditorGUI.DrawRect(rect, bgColor);

                GUILayout.Label(entry.Word, GUILayout.Width(180));
                GUILayout.Label(entry.Pronunciation, GUILayout.Width(180));

                DrawPriorityBar(entry.Priority);

                GUILayout.Label(entry.FileName, EditorStyles.miniLabel, GUILayout.Width(160));

                if (entry.IsEditable)
                {
                    if (GUILayout.Button("Edit", EditorStyles.miniButton, GUILayout.Width(36)))
                    {
                        BeginEditEntry(entry);
                    }

                    if (GUILayout.Button("Del", EditorStyles.miniButton, GUILayout.Width(36)))
                    {
                        DeleteEntry(entry);
                    }
                }
                else
                {
                    GUILayout.Space(78);
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        // --- Priority Color Bar ---

        private static void DrawPriorityBar(int priority)
        {
            var barRect = GUILayoutUtility.GetRect(60, 16, GUILayout.Width(60));
            var fillWidth = barRect.width * (priority / 10f);

            EditorGUI.DrawRect(barRect, new Color(0.2f, 0.2f, 0.2f, 0.3f));

            Color fillColor;
            if (priority <= 3)
                fillColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);    // gray
            else if (priority <= 6)
                fillColor = new Color(0.2f, 0.4f, 0.8f, 0.7f);    // blue
            else if (priority <= 8)
                fillColor = new Color(0.9f, 0.6f, 0.1f, 0.7f);    // orange
            else
                fillColor = new Color(0.9f, 0.2f, 0.2f, 0.7f);    // red

            EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, fillWidth, barRect.height), fillColor);
            GUI.Label(barRect, priority.ToString(), EditorStyles.miniLabel);
        }

        // --- Edit Panel ---

        private void DrawEditPanel()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label(_isEditing ? "Edit Entry" : "Add Entry", _headerStyle);

            _editWord = EditorGUILayout.TextField("Word", _editWord);
            _editPronunciation = EditorGUILayout.TextField("Pronunciation", _editPronunciation);
            _editPriority = EditorGUILayout.IntSlider("Priority", _editPriority, 1, 10);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            var buttonLabel = _isEditing ? "Update" : "Add";
            if (GUILayout.Button(buttonLabel, GUILayout.Width(80)))
            {
                SaveEditEntry();
            }

            if (GUILayout.Button("Cancel", GUILayout.Width(80)))
            {
                _showEditPanel = false;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        // --- Preview Panel ---

        private void DrawPreviewPanel()
        {
            EditorGUILayout.Space(4);
            _showPreviewPanel = EditorGUILayout.Foldout(_showPreviewPanel, "Dictionary Preview", true);
            if (!_showPreviewPanel) return;

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            _previewInputText = EditorGUILayout.TextField("Input Text", _previewInputText);
            if (GUILayout.Button("Preview", GUILayout.Width(70)))
            {
                RunPreview();
            }

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_previewResultText))
            {
                EditorGUILayout.LabelField("Result", _previewResultText);

                if (_previewReplacements.Count > 0)
                {
                    _previewScroll = EditorGUILayout.BeginScrollView(_previewScroll, GUILayout.MaxHeight(120));
                    foreach (var r in _previewReplacements)
                    {
                        EditorGUILayout.LabelField(
                            $"  \"{r.OriginalWord}\" -> \"{r.Pronunciation}\" (priority={r.Priority}, pos={r.Position})");
                    }

                    EditorGUILayout.EndScrollView();
                }
            }

            EditorGUILayout.EndVertical();
        }

        // --- Data Operations ---

        private void ReloadAll()
        {
            _allEntries.Clear();
            _fileWriteTimes.Clear();

            var dictDir = Path.Combine(Application.streamingAssetsPath, "uPiper", "Dictionaries");
            if (!Directory.Exists(dictDir))
            {
                _filteredEntries.Clear();
                RebuildFileFilterLabels();
                return;
            }

            var jsonFiles = Directory.GetFiles(dictDir, "*.json")
                .OrderBy(Path.GetFileName)
                .ToArray();

            foreach (var filePath in jsonFiles)
            {
                try
                {
                    var fileName = Path.GetFileName(filePath);
                    var isEditable = string.Equals(fileName, UserCustomDictFileName,
                        StringComparison.OrdinalIgnoreCase);
                    var entries = DictionaryJsonEditor.ReadEntries(filePath);

                    _fileWriteTimes[filePath] = File.GetLastWriteTimeUtc(filePath);

                    foreach (var kvp in entries)
                    {
                        _allEntries.Add(new EntryView
                        {
                            Word = kvp.Key,
                            Pronunciation = kvp.Value.pronunciation,
                            Priority = kvp.Value.priority,
                            FileName = fileName,
                            IsEditable = isEditable
                        });
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DictionaryManager] Failed to load {filePath}: {e.Message}");
                }
            }

            RebuildFileFilterLabels();
            ApplyFilters();
        }

        private void RebuildFileFilterLabels()
        {
            var fileNames = _allEntries.Select(e => e.FileName).Distinct().OrderBy(n => n).ToList();
            var labels = new List<string> { "All" };
            labels.AddRange(fileNames);
            _fileFilterLabels = labels.ToArray();

            if (_fileFilterIndex >= _fileFilterLabels.Length)
                _fileFilterIndex = 0;
        }

        private void ApplyFilters()
        {
            _filteredEntries = _allEntries.Where(e =>
            {
                // File filter
                if (_fileFilterIndex > 0 && _fileFilterIndex < _fileFilterLabels.Length)
                {
                    if (!string.Equals(e.FileName, _fileFilterLabels[_fileFilterIndex],
                            StringComparison.OrdinalIgnoreCase))
                        return false;
                }

                // Priority filter
                switch (_priorityFilterIndex)
                {
                    case 1 when e.Priority > 3: return false;
                    case 2 when (e.Priority < 4 || e.Priority > 6): return false;
                    case 3 when (e.Priority < 7 || e.Priority > 8): return false;
                    case 4 when e.Priority < 9: return false;
                }

                // Text search (word + pronunciation partial match)
                if (!string.IsNullOrEmpty(_searchText))
                {
                    var lower = _searchText.ToLower();
                    if (!e.Word.ToLower().Contains(lower) &&
                        !e.Pronunciation.ToLower().Contains(lower))
                        return false;
                }

                return true;
            }).ToList();
        }

        // --- Edit Lifecycle ---

        private void BeginAddEntry()
        {
            _showEditPanel = true;
            _isEditing = false;
            _editWord = "";
            _editPronunciation = "";
            _editPriority = 5;
            _editOriginalWord = "";
        }

        private void BeginEditEntry(EntryView entry)
        {
            _showEditPanel = true;
            _isEditing = true;
            _editWord = entry.Word;
            _editPronunciation = entry.Pronunciation;
            _editPriority = entry.Priority;
            _editOriginalWord = entry.Word;
        }

        private void SaveEditEntry()
        {
            if (string.IsNullOrWhiteSpace(_editWord) || string.IsNullOrWhiteSpace(_editPronunciation))
            {
                EditorUtility.DisplayDialog("Validation Error",
                    "Word and Pronunciation must not be empty.", "OK");
                return;
            }

            var filePath = GetUserCustomDictPath();
            if (!CheckConcurrency(filePath)) return;

            try
            {
                // If editing and the word changed, remove the old entry
                if (_isEditing && _editOriginalWord != _editWord)
                {
                    DictionaryJsonEditor.RemoveEntry(filePath, _editOriginalWord);
                }

                DictionaryJsonEditor.UpsertEntry(filePath, _editWord, _editPronunciation, _editPriority);
                _showEditPanel = false;
                ReloadAll();
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Save Error", e.Message, "OK");
            }
        }

        private void DeleteEntry(EntryView entry)
        {
            if (!EditorUtility.DisplayDialog("Delete Entry",
                    $"Delete \"{entry.Word}\"?", "Delete", "Cancel"))
                return;

            var filePath = GetUserCustomDictPath();
            if (!CheckConcurrency(filePath)) return;

            try
            {
                DictionaryJsonEditor.RemoveEntry(filePath, entry.Word);
                ReloadAll();
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Delete Error", e.Message, "OK");
            }
        }

        // --- Import / Export ---

        private void ImportJson()
        {
            var path = EditorUtility.OpenFilePanel("Import Dictionary JSON", "", "json");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                var imported = DictionaryJsonEditor.ReadEntries(path);
                if (imported.Count == 0)
                {
                    EditorUtility.DisplayDialog("Import",
                        "No entries found in the selected file.", "OK");
                    return;
                }

                var filePath = GetUserCustomDictPath();
                if (!CheckConcurrency(filePath)) return;

                var existing = File.Exists(filePath)
                    ? DictionaryJsonEditor.ReadEntries(filePath)
                    : new Dictionary<string, (string, int)>();

                foreach (var kvp in imported)
                {
                    existing[kvp.Key] = kvp.Value;
                }

                DictionaryJsonEditor.ExportToJson(filePath, existing);
                ReloadAll();

                EditorUtility.DisplayDialog("Import",
                    $"Imported {imported.Count} entries into {UserCustomDictFileName}.", "OK");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Import Error", e.Message, "OK");
            }
        }

        private void ExportJson()
        {
            var path = EditorUtility.SaveFilePanel("Export Dictionary JSON", "", "dictionary_export.json", "json");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                // Export all entries (all files merged)
                var allMap = new Dictionary<string, (string pronunciation, int priority)>();
                foreach (var entry in _allEntries)
                {
                    allMap[entry.Word] = (entry.Pronunciation, entry.Priority);
                }

                DictionaryJsonEditor.ExportToJson(path, allMap);
                EditorUtility.DisplayDialog("Export",
                    $"Exported {allMap.Count} entries.", "OK");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Export Error", e.Message, "OK");
            }
        }

        // --- Preview ---

        private void RunPreview()
        {
            if (string.IsNullOrEmpty(_previewInputText))
            {
                _previewResultText = "";
                _previewReplacements = Array.Empty<CustomDictionary.ReplacementDetail>();
                return;
            }

            try
            {
                var dict = new CustomDictionary(loadDefaults: true);
                var (resultText, replacements) = dict.ApplyToTextWithDetails(_previewInputText);
                _previewResultText = resultText;
                _previewReplacements = replacements;
            }
            catch (Exception e)
            {
                _previewResultText = $"Error: {e.Message}";
                _previewReplacements = Array.Empty<CustomDictionary.ReplacementDetail>();
            }
        }

        // --- Utilities ---

        private static string GetUserCustomDictPath()
        {
            return Path.Combine(Application.streamingAssetsPath, "uPiper", "Dictionaries", UserCustomDictFileName);
        }

        /// <summary>
        /// ファイル書き込み前に並行編集を検出する。
        /// </summary>
        private bool CheckConcurrency(string filePath)
        {
            if (!File.Exists(filePath)) return true;

            var currentWriteTime = File.GetLastWriteTimeUtc(filePath);
            if (_fileWriteTimes.TryGetValue(filePath, out var knownWriteTime) &&
                currentWriteTime != knownWriteTime)
            {
                var proceed = EditorUtility.DisplayDialog("File Changed",
                    $"{Path.GetFileName(filePath)} was modified externally. Reload and discard your change?",
                    "Reload", "Cancel");
                if (proceed)
                {
                    ReloadAll();
                }

                return false;
            }

            return true;
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            _toolbarButtonStyle = new GUIStyle(EditorStyles.toolbarButton);
            _stylesInitialized = true;
        }
    }
}