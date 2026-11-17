// Copyright (c) Bian Shanghai
// https://github.com/Bian-Sh/Script-Encoding-Converter
// Licensed under the MIT license. See the LICENSE file in the package root for more information.

using System;
using System.IO;
using System.Text;

namespace MUD.Modules.ScriptEncodingConverter
{
    /// <summary> Converts a single text file fully in memory before any bytes hit the disk. </summary>
    internal static class FileConverter
    {
        /// <summary>
        /// Reads <paramref name="path"/> strictly, encodes the result fully in memory, backs up the
        /// original when <paramref name="backupDirectory"/> is not null, then overwrites the file.
        /// On any failure the original file is left byte-for-byte intact.
        /// </summary>
        /// <param name="path">Project-relative asset path.</param>
        /// <param name="from">Source encoding (exception fallbacks expected).</param>
        /// <param name="to">Target encoding.</param>
        /// <param name="backupDirectory">Directory of the current batch backup session, or null to disable backup.</param>
        internal static bool TryConvert(string path, Encoding from, Encoding to, string backupDirectory, out string error)
        {
            error = null;

            string text;
            try
            {
                text = File.ReadAllText(path, from);
            }
            catch (Exception e) when (IsExpectedException(e))
            {
                error = $"read failed: {e.Message}";
                return false;
            }

            byte[] outBytes;
            try
            {
                // Encode fully in memory first — if a character has no target-encoding representation,
                // nothing is written to disk and the original file stays intact.
                outBytes = to.GetBytes(text);
            }
            catch (System.Text.EncoderFallbackException e)
            {
                error = $"character cannot be represented in the target encoding: {e.Message}";
                return false;
            }

            if (backupDirectory != null)
            {
                try
                {
                    var mirror = GetMirrorPath(backupDirectory, path);
                    Directory.CreateDirectory(Path.GetDirectoryName(mirror));
                    File.Copy(path, mirror, overwrite: true);
                }
                catch (Exception e) when (IsExpectedException(e))
                {
                    // Never overwrite while the requested backup could not be made.
                    error = $"backup failed: {e.Message}";
                    return false;
                }
            }

            try
            {
                File.WriteAllBytes(path, outBytes);
                return true;
            }
            catch (Exception e) when (IsExpectedException(e))
            {
                error = $"write failed: {e.Message}";
                return false;
            }
        }

        static bool IsExpectedException(Exception e)
            => e is DecoderFallbackException
            || e is System.Text.EncoderFallbackException
            || e is IOException
            || e is UnauthorizedAccessException
            || e is ArgumentException
            || e is NotSupportedException
            || e is PathTooLongException;

        internal static string GetMirrorPath(string backupDirectory, string assetPath)
        {
            var root = Path.GetFullPath(backupDirectory);
            // Relative asset paths keep their folder structure under the backup root;
            // absolute paths are mirrored by file name only.
            var rel = assetPath.Replace('/', Path.DirectorySeparatorChar);
            var mirror = Path.IsPathRooted(rel)
                ? Path.GetFullPath(Path.Combine(root, Path.GetFileName(rel)))
                : Path.GetFullPath(Path.Combine(root, rel));
            var rootWithSlash = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!mirror.StartsWith(rootWithSlash, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("asset path escapes the backup directory", nameof(assetPath));
            }
            return mirror;
        }
    }
}
