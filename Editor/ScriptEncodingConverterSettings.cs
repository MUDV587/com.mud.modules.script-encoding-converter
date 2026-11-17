// Copyright (c) Bian Shanghai
// https://github.com/Bian-Sh/Script-Encoding-Converter
// Licensed under the MIT license. See the LICENSE file in the package root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace MUD.Modules.ScriptEncodingConverter
{
    /// <summary>
    /// Per-project settings, stored in ProjectSettings/ScriptEncodingConverter.asset (ScriptableSingleton).
    /// The asset is meant to be committed to version control and shared by the whole team.
    /// On first access the asset is created with sensible defaults; afterwards the SupportedExtensions
    /// list is managed entirely by the user ("full takeover" mode).
    /// </summary>
    [FilePath("ProjectSettings/ScriptEncodingConverter.asset", FilePathAttribute.Location.ProjectFolder)]
    public class ScriptEncodingConverterSettings : ScriptableSingleton<ScriptEncodingConverterSettings>
    {
        public static readonly string[] DefaultSupportedExtensions =
        {
            ".cs", ".shader", ".cginc", ".hlsl", ".compute", ".asmdef", ".uxml", ".uss", ".txt", ".md"
        };

        static readonly Regex ValidExtension = new Regex(@"^\.[A-Za-z0-9][A-Za-z0-9]*$", RegexOptions.Compiled);

        public bool EnableAutoFix;
        public bool EnableBackup = true;
        public string BackupDirectory = string.Empty;
        public List<string> SupportedExtensions = new List<string>(DefaultSupportedExtensions);

        // Returns the singleton, creating and seeding the asset (with defaults) when missing.
        public static ScriptEncodingConverterSettings GetOrCreate()
        {
            var settings = instance;
            if (!File.Exists(GetFilePath()))
            {
                // Freshly created in-memory object: normalize and persist the seeded defaults.
                settings.NormalizeExtensions();
                settings.Save(true);
            }
            return settings;
        }

        /// <summary> Normalizes extension entries in-place (trim, lowercase, single leading dot, dedupe, drop invalid). </summary>
        internal void NormalizeExtensions()
        {
            SupportedExtensions = NormalizeExtensionList(SupportedExtensions);
        }

        /// <summary>
        /// Pure list-normalization used by <see cref="NormalizeExtensions"/>: trim, lowercase, single leading dot,
        /// dedupe, drop invalid entries. Null falls back to the default list. Extracted so it can be unit-tested
        /// without a ScriptableSingleton instance.
        /// </summary>
        internal static List<string> NormalizeExtensionList(IEnumerable<string> source)
        {
            if (source == null)
            {
                return new List<string>(DefaultSupportedExtensions);
            }

            var normalized = new List<string>();
            foreach (var raw in source)
            {
                var e = (raw ?? string.Empty).Trim().ToLowerInvariant();
                if (e.Length == 0) continue;
                if (!e.StartsWith(".")) e = "." + e;
                if (e.Length == 1) continue;
                if (e.IndexOf('.', 1) >= 0)
                {
                    // Looks like "file.ext" — keep only the extension part.
                    var ext = Path.GetExtension(e);
                    if (ext.Length == 0) continue;
                    e = ext;
                }
                if (!ValidExtension.IsMatch(e)) continue;
                // All entries are lowercase at this point, so ordinal Contains is case-insensitive here.
                if (!normalized.Contains(e))
                {
                    normalized.Add(e);
                }
            }
            return normalized;
        }

        static string ProjectRootPath => Directory.GetParent(Application.dataPath).FullName;

        /// <summary> Resolved backup root: empty setting → <temp>/ScriptEncodingConverterBackups/<project name>; relative → project root. </summary>
        internal string GetBackupRoot()
        {
            var p = (BackupDirectory ?? string.Empty).Trim();
            if (p.Length == 0)
            {
                var project = new DirectoryInfo(ProjectRootPath).Name;
                return Path.Combine(Path.GetTempPath(), "ScriptEncodingConverterBackups", project);
            }
            if (!Path.IsPathRooted(p)) p = Path.Combine(ProjectRootPath, p);
            return p;
        }

        /// <summary> Creates a timestamped session backup directory and returns its full path. </summary>
        internal string CreateBackupSessionDirectory()
        {
            var baseDir = Path.Combine(GetBackupRoot(), DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff"));
            var dir = baseDir;
            for (var i = 2; Directory.Exists(dir); i++)
            {
                // Another batch started in the same millisecond — never share a session directory.
                dir = baseDir + "-" + i;
            }
            Directory.CreateDirectory(dir);
            return dir;
        }

        // Persists the current in-memory state, normalizing the extension list first.
        static void Save()
        {
            instance.NormalizeExtensions();
            instance.Save(true);
        }

        // ------------------------------------------------------------------
        // SettingsProvider: Project Settings window page.
        // Everything is drawn through a SerializedObject so undo/redo
        // (Ctrl+Z / Ctrl+Y) works natively and the asset stays in sync.
        // ------------------------------------------------------------------
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider("Project/Script Encoding Converter", SettingsScope.Project)
            {
                label = "Script Encoding Converter",
                guiHandler = DrawGui,
                keywords = new HashSet<string> { "encoding", "gb2312", "utf-8", "utf8", "auto fix", "backup", "extension", "charset" },
            };
        }

        static void DrawGui(string searchContext)
        {
            var settings = GetOrCreate();
            var so = new SerializedObject(settings);
            so.Update();

            var autoFixProp = so.FindProperty(nameof(EnableAutoFix));
            var backupProp = so.FindProperty(nameof(EnableBackup));
            var backupDirectoryProp = so.FindProperty(nameof(BackupDirectory));
            var extensionsProp = so.FindProperty(nameof(SupportedExtensions));

            // Save and restore the global label width so we don't leak 220f into other editors.
            var previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 220f;

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Auto Fix", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(autoFixProp, new GUIContent("Auto fix on import / move"));
            EditorGUILayout.LabelField("Applies to the supported extensions listed below. Works per project — this asset is meant to be committed to version control.", EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Backup", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(backupProp, new GUIContent("Back up before conversion"));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(backupDirectoryProp, new GUIContent("Backup directory"));
            if (GUILayout.Button("Browse…", GUILayout.Width(70f)))
            {
                var picked = EditorUtility.OpenFolderPanel("Select Backup Directory", settings.GetBackupRoot(), string.Empty);
                if (!string.IsNullOrEmpty(picked))
                {
                    backupDirectoryProp.stringValue = picked;
                    GUI.FocusControl(null);
                }
            }
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Open Backup Directory", GUILayout.Width(180f)))
            {
                try
                {
                    var dir = settings.GetBackupRoot();
                    Directory.CreateDirectory(dir);
                    EditorUtility.RevealInFinder(dir);
                }
                catch (Exception e) when (IsExpectedException(e))
                {
                    Debug.LogError($"Script Encoding Converter: cannot open backup directory — {e.Message}");
                }
            }
            EditorGUILayout.HelpBox(
                "Backups are written before any file is modified. Leave the Backup directory empty to use the default location: " +
                "the system temp folder under ScriptEncodingConverterBackups, named after your project. " +
                "Files in the temp folder may be cleaned by the OS; a custom directory is your responsibility. " +
                "Relative paths are resolved from the project root.",
                MessageType.Info);

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Supported Extensions", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("The list is used as-is (full takeover). Manual conversion, Auto Fix and the Preview menu all honor it.", EditorStyles.wordWrappedMiniLabel);
            DrawExtensionList(so, extensionsProp);

            if (GUILayout.Button("Reset to Defaults", GUILayout.Width(140f)))
            {
                extensionsProp.ClearArray();
                foreach (var ext in DefaultSupportedExtensions)
                {
                    extensionsProp.arraySize++;
                    extensionsProp.GetArrayElementAtIndex(extensionsProp.arraySize - 1).stringValue = ext;
                }
                GUI.FocusControl(null);
            }

            if (so.ApplyModifiedProperties())
            {
                settings.NormalizeExtensions();
                settings.Save(true);
            }

            if (settings.SupportedExtensions == null || settings.SupportedExtensions.Count == 0)
            {
                EditorGUILayout.HelpBox("The supported extensions list is empty. No files will be converted until at least one extension is added.", MessageType.Warning);
            }

            EditorGUIUtility.labelWidth = previousLabelWidth;
        }

        static void DrawExtensionList(SerializedObject so, SerializedProperty listProp)
        {
            var listGui = new ReorderableList(so, listProp, draggable: true, displayHeader: true, displayAddButton: true, displayRemoveButton: true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Extensions"),
                elementHeight = EditorGUIUtility.singleLineHeight + 2f,
                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    rect.y += 1f;
                    var element = listProp.GetArrayElementAtIndex(index);
                    EditorGUI.PropertyField(rect, element, GUIContent.none);
                },
                onAddCallback = list =>
                {
                    listProp.arraySize++;
                    listProp.GetArrayElementAtIndex(listProp.arraySize - 1).stringValue = ".";
                },
            };
            listGui.DoLayoutList();
        }

        static bool IsExpectedException(Exception e)
            => e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException || e is PathTooLongException;
    }
}
