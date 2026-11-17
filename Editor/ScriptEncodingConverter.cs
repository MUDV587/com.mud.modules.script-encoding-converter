// Copyright (c) Bian Shanghai
// https://github.com/Bian-Sh/Script-Encoding-Converter
// Licensed under the MIT license. See the LICENSE file in the package root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEditor;
using UnityEngine;

[assembly: InternalsVisibleTo("MUD.Modules.ScriptEncodingConverter.Tests.Editor")]

namespace MUD.Modules.ScriptEncodingConverter
{
    public class ScriptEncodingConverter
    {
        const string menu_to_utf8 = "Assets/Script Encoding Converter/To UTF8";
        const string menu_to_gb2312 = "Assets/Script Encoding Converter/To GB2312";
        const string menu_preview = "Assets/Script Encoding Converter/Preview";
        const int MaxListedFiles = 50;

        // true while a user-triggered batch conversion is running (suppresses Auto Fix callbacks)
        static bool isConvertManually;
        internal static bool IsConvertingManually { get => isConvertManually; set => isConvertManually = value; }

        // -------------------------------------------------------------
        // Conversion menus
        // -------------------------------------------------------------
        [MenuItem(menu_to_utf8)]
        static void Convert2UTF8()
        {
            var settings = new ConvertSettings
            {
                target = FileEncodingKind.Gb2312,
                from = EncodingDetector.GetGb2312(),
                to = new UTF8Encoding(false),
                fromName = "GB2312",
                toName = "UTF-8",
                reportUnidentified = true,
                title = "Manual conversion",
            };
            EncodingConverter(settings);
        }

        [MenuItem(menu_to_gb2312)]
        static void Convert2GB2312()
        {
            var settings = new ConvertSettings
            {
                // Pure ASCII files are already valid GB2312 — converting them is a no-op rewrite,
                // so only genuine non-ASCII UTF-8 files are targeted.
                target = FileEncodingKind.Utf8,
                // A file that fails to decode as strict UTF-8 is skipped instead of being corrupted.
                from = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                // EncoderFallbackException (e.g. emoji) is raised before anything is written; the file stays intact.
                to = EncodingDetector.GetGb2312(),
                fromName = "UTF-8",
                toName = "GB2312",
                reportUnidentified = false,
                title = "Manual conversion",
            };
            EncodingConverter(settings);
        }

        // Menus are enabled only when the selection contains supported text assets.
        [MenuItem(menu_to_utf8, validate = true)]
        [MenuItem(menu_to_gb2312, validate = true)]
        [MenuItem(menu_preview, validate = true)]
        static bool ConvertMenuValidate() => HasSupportedSelection();

        static bool HasSupportedSelection()
        {
            foreach (var guid in Selection.assetGUIDs)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path)) return true; // folders may contain text assets
                if (IsSupportedTextAsset(path)) return true;
            }
            return false;
        }

        /// <summary> True when <paramref name="path"/> ends with a supported text-asset extension (from Settings). </summary>
        internal static bool IsSupportedTextAsset(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var extension = Path.GetExtension(path);
            if (string.IsNullOrEmpty(extension)) return false;
            var list = ScriptEncodingConverterSettings.GetOrCreate().SupportedExtensions;
            if (list == null) return false;
            foreach (var e in list)
            {
                if (string.Equals(extension, e, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        static string _projectRoot;

        static string ProjectRoot => _projectRoot ?? (_projectRoot =
            new DirectoryInfo(Application.dataPath).Parent.FullName.Replace('\\', '/') + "/");

        /// <summary> True for paths under the project's Library folder (incl. PackageCache) — those are read-only. </summary>
        internal static bool IsReadOnlyPackageAsset(string path)
        {
            // Resolve relative paths against the project root (not the process CWD, which is not guaranteed).
            var combined = Path.IsPathRooted(path) ? path : Path.Combine(ProjectRoot, path);
            var fullPath = Path.GetFullPath(combined).Replace('\\', '/');
            return fullPath.StartsWith(ProjectRoot + "Library/", StringComparison.OrdinalIgnoreCase);
        }

        // -------------------------------------------------------------
        // Preview (dry-run) menu
        // -------------------------------------------------------------
        [MenuItem(menu_preview)]
        static void Preview()
        {
            var paths = SelectSupportedTextPaths();
            var wouldConvert = new List<string>();
            var unidentified = new List<string>();
            foreach (var path in paths)
            {
                var kind = EncodingDetector.ProbeFileEncoding(path);
                if (kind == FileEncodingKind.Gb2312)
                {
                    wouldConvert.Add(path);
                }
                else if (kind == FileEncodingKind.Unidentified)
                {
                    unidentified.Add(path);
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Preview (no files will be modified) — {paths.Count} supported file(s) in selection");
            sb.AppendLine($"  Would convert {wouldConvert.Count} file(s) from GB2312 to UTF-8" + (wouldConvert.Count > 0 ? ":" : "."));
            foreach (var path in wouldConvert.Take(MaxListedFiles))
            {
                sb.AppendLine("    GB2312 → UTF-8: " + Link(path));
            }
            if (wouldConvert.Count > MaxListedFiles)
            {
                sb.AppendLine($"    ...and {wouldConvert.Count - MaxListedFiles} more");
            }
            sb.AppendLine($"  Cannot identify encoding, {unidentified.Count} file(s) would be skipped" + (unidentified.Count > 0 ? ":" : "."));
            foreach (var path in unidentified.Take(MaxListedFiles))
            {
                sb.AppendLine("    " + Link(path));
            }
            if (unidentified.Count > MaxListedFiles)
            {
                sb.AppendLine($"    ...and {unidentified.Count - MaxListedFiles} more");
            }
            Debug.Log(sb.ToString());
        }

        // -------------------------------------------------------------
        // Batch conversion
        // -------------------------------------------------------------
        static List<string> SelectSupportedTextPaths()
        {
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in Selection.GetFiltered<UnityEngine.Object>(SelectionMode.DeepAssets))
            {
                var path = AssetDatabase.GetAssetPath(item);
                if (IsSupportedTextAsset(path) && !IsReadOnlyPackageAsset(path))
                {
                    unique.Add(path);
                }
            }
            return unique.ToList();
        }

        // Resolved per-file-loop to get the batch backup directory (created lazily, once per batch).
        static string ResolveBackupDirectoryForBatch(bool enableBackup, ref string backupDir, ref string backupError)
        {
            if (!enableBackup || backupDir != null || backupError != null) return backupDir;
            try
            {
                backupDir = ScriptEncodingConverterSettings.GetOrCreate().CreateBackupSessionDirectory();
            }
            catch (Exception e)
            {
                backupError = e.Message;
            }
            return backupDir;
        }

        static void EncodingConverter(ConvertSettings settings)
        {
            var paths = SelectSupportedTextPaths();
            if (paths.Count == 0) return;

            var cfg = ScriptEncodingConverterSettings.GetOrCreate();
            isConvertManually = true;
            try
            {
                string backupDir = null;
                string backupError = null;

                var files = new List<string>();
                var failed = new List<string>();
                var unidentified = new List<string>();
                foreach (var path in paths)
                {
                    var kind = EncodingDetector.ProbeFileEncoding(path);
                    if (kind != settings.target)
                    {
                        if (settings.reportUnidentified && kind == FileEncodingKind.Unidentified)
                        {
                            unidentified.Add(path);
                        }
                        continue;
                    }

                    ResolveBackupDirectoryForBatch(cfg.EnableBackup, ref backupDir, ref backupError);
                    if (backupError != null)
                    {
                        Debug.LogError($"Script Encoding Converter: backup is enabled but the backup directory cannot be created ({backupError}). No more files will be converted.");
                        break;
                    }

                    if (FileConverter.TryConvert(path, settings.from, settings.to, backupDir, out var error))
                    {
                        files.Add(path);
                    }
                    else
                    {
                        failed.Add(path);
                        Debug.LogWarning($"Script Encoding Converter: failed to convert {path} — {error}");
                    }
                }

                if (files.Count == 0 && failed.Count == 0 && unidentified.Count == 0)
                {
                    // Nothing happened — stay silent.
                    return;
                }

                Report(settings.title, files, failed, unidentified, settings.fromName, settings.toName);
                if (files.Count > 0)
                {
                    AssetDatabase.Refresh();
                }
            }
            finally
            {
                isConvertManually = false;
            }
        }

        // -------------------------------------------------------------
        // Reporting (English only; quiet when no conversion happened)
        // -------------------------------------------------------------
        internal static void Report(string title, IEnumerable<string> converted, IEnumerable<string> failed, IEnumerable<string> unidentified, string fromName, string toName)
        {
            var convertedList = converted?.ToList() ?? new List<string>();
            var failedList = failed?.ToList() ?? new List<string>();
            var unidentifiedList = unidentified?.ToList() ?? new List<string>();

            if (convertedList.Count > 0)
            {
                var lines = convertedList.Take(MaxListedFiles).Select(v => $"{fromName} → {toName}: {Link(v)}");
                Debug.Log($"{title}: Converted {convertedList.Count} file(s) from {fromName} to {toName} ↓ \n{string.Join("\n", lines)}{Tail(convertedList.Count)}");
            }

            if (failedList.Count > 0)
            {
                var lines = failedList.Take(MaxListedFiles).Select(Link);
                Debug.LogWarning($"{title}: Failed to convert {failedList.Count} file(s) ↓ \n{string.Join("\n", lines)}{Tail(failedList.Count)}");
            }

            if (unidentifiedList.Count > 0)
            {
                var lines = unidentifiedList.Take(MaxListedFiles).Select(Link);
                Debug.LogWarning($"{title}: Cannot identify encoding, skipped {unidentifiedList.Count} file(s) ↓ \n{string.Join("\n", lines)}{Tail(unidentifiedList.Count)}");
            }
        }

        static string Link(string path) => $"<a sourcefile=\"{path}\">{path}</a>";

        static string Tail(int total) => total > MaxListedFiles ? $"\n...and {total - MaxListedFiles} more" : string.Empty;

        class ConvertSettings
        {
            public FileEncodingKind target;
            public Encoding from, to;
            public string fromName, toName, title;
            public bool reportUnidentified;
        }
    }
}
