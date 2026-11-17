// Copyright (c) Bian Shanghai
// https://github.com/Bian-Sh/Script-Encoding-Converter
// Licensed under the MIT license. See the LICENSE file in the package root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MUD.Modules.ScriptEncodingConverter
{
    /// <summary> Queues problematic text assets seen during import and fixes them after the import completes. </summary>
    internal class ScriptEncodingAutoFixHandler : AssetPostprocessor
    {
        static readonly HashSet<string> pendingFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static bool isScheduled;

        // Called for every asset import/delete/move. Must stay cheap — the heavy work happens in delayCall.
        public static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (ScriptEncodingConverter.IsConvertingManually) return;
            if (!ScriptEncodingConverterSettings.GetOrCreate().EnableAutoFix) return;

            var scripts = importedAssets.Concat(movedAssets)
                .Where(ScriptEncodingConverter.IsSupportedTextAsset)
                .Where(v => !ScriptEncodingConverter.IsReadOnlyPackageAsset(v))
                .ToArray();
            if (scripts.Length == 0) return;

            pendingFiles.UnionWith(scripts);
            if (isScheduled) return;
            isScheduled = true;
            // Never rewrite files while Unity is still importing them; run after the import completes.
            EditorApplication.delayCall += ProcessPendingFiles;
        }

        static void ProcessPendingFiles()
        {
            // Guarantee one-shot semantics regardless of Unity's delayCall details.
            EditorApplication.delayCall -= ProcessPendingFiles;
            isScheduled = false;

            var settings = ScriptEncodingConverterSettings.GetOrCreate();
            var candidates = new List<string>(pendingFiles);
            pendingFiles.Clear();
            if (!settings.EnableAutoFix) return;

            // Created lazily on the first file that actually needs conversion — never for no-op batches.
            string backupDir = null;
            string backupError = null;
            string ResolveBackupDirectory()
            {
                if (!settings.EnableBackup || backupDir != null || backupError != null) return backupDir;
                try
                {
                    backupDir = settings.CreateBackupSessionDirectory();
                }
                catch (Exception e)
                {
                    backupError = e.Message;
                }
                return backupDir;
            }

            var converted = new List<string>();
            var failed = new List<string>();
            var unidentified = new List<string>();
            foreach (var path in candidates)
            {
                // The asset may have been deleted or moved in the meantime.
                if (!File.Exists(path)) continue;
                var kind = EncodingDetector.ProbeFileEncoding(path);
                if (kind != FileEncodingKind.Gb2312)
                {
                    if (kind == FileEncodingKind.Unidentified) unidentified.Add(path);
                    continue;
                }
                ResolveBackupDirectory();
                if (backupError != null)
                {
                    Debug.LogError($"Script Encoding Converter: backup is enabled but the backup directory cannot be created ({backupError}). No more files will be converted.");
                    break;
                }
                if (FileConverter.TryConvert(path, EncodingDetector.GetGb2312(), new UTF8Encoding(false), backupDir, out var error))
                {
                    converted.Add(path);
                }
                else
                {
                    failed.Add(path);
                    Debug.LogWarning($"Script Encoding Converter: Auto Fix failed for {path} — {error}");
                }
            }

            if (converted.Count > 0 || failed.Count > 0 || unidentified.Count > 0)
            {
                ScriptEncodingConverter.Report("Auto Fix to UTF8", converted, failed, unidentified, "GB2312", "UTF-8");
            }
            if (converted.Count > 0)
            {
                // Suppress the Auto Fix callback for the assets we are about to re-import — otherwise
                // every refreshed file is queued and re-scanned for no benefit.
                ScriptEncodingConverter.IsConvertingManually = true;
                try
                {
                    AssetDatabase.Refresh();
                }
                finally
                {
                    ScriptEncodingConverter.IsConvertingManually = false;
                }
            }
        }
    }
}
