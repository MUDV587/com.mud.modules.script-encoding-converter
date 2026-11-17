// Copyright (c) Bian Shanghai
// https://github.com/Bian-Sh/Script-Encoding-Converter
// Licensed under the MIT license. See the LICENSE file in the package root for more information.

using System;
using System.IO;
using System.Text;

namespace MUD.Modules.ScriptEncodingConverter
{
    /// <summary> How a file's encoding was classified by the single-read probe. </summary>
    internal enum FileEncodingKind
    {
        /// <summary> File could not be read (missing / locked / access denied). </summary>
        Unreadable,
        /// <summary> Pure ASCII (no non-ASCII and no NUL bytes). </summary>
        Ascii,
        /// <summary> UTF-8 without a BOM, with non-ASCII bytes. </summary>
        Utf8,
        /// <summary> Not valid UTF-8 but fully decodable as GB2312. </summary>
        Gb2312,
        /// <summary>
        /// Neither valid UTF-8 nor valid GB2312 — including files with NUL bytes (UTF-16/UTF-32 or binary),
        /// which are never safe to convert with this tool.
        /// </summary>
        Unidentified,
    }

    /// <summary> File encoding detection (BOM + strict decode roundtrip). </summary>
    public static class EncodingDetector
    {
        // Lazily created so the CodePages provider is registered (static ctor) before any 936 lookup.
        static Encoding _strictUtf8;
        static Encoding _strictGb2312;
        static Encoding StrictUtf8 => _strictUtf8 ?? (_strictUtf8 = GetVerifier("utf-8"));
        static Encoding StrictGb2312 => _strictGb2312 ?? (_strictGb2312 = GetGb2312());

        static EncodingDetector()
        {
            EnsureCodePagesRegistered();
        }

        /// <summary> Register the CodePages provider on runtimes that need it (no-op on Unity Mono). </summary>
        internal static void EnsureCodePagesRegistered()
        {
            try
            {
                var providerType = Type.GetType("System.Text.CodePagesEncodingProvider, System.Text.Encoding.CodePages")
                                ?? Type.GetType("System.Text.CodePagesEncodingProvider, netstandard")
                                ?? Type.GetType("System.Text.CodePagesEncodingProvider, System")
                                ?? Type.GetType("System.Text.CodePagesEncodingProvider, mscorlib");
                if (providerType == null)
                {
                    // Fallback: scan already-loaded assemblies to cover per-Unity layouts.
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        providerType = asm.GetType("System.Text.CodePagesEncodingProvider", false);
                        if (providerType != null) break;
                    }
                }
                var provider = providerType?
                    .GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?
                    .GetValue(null) as EncodingProvider;
                if (provider != null)
                {
                    Encoding.RegisterProvider(provider);
                }
            }
            catch (Exception)
            {
                // Already registered or unavailable — harmless either way.
            }
        }

        /// <summary> CP936/GB2312 with exception fallbacks (never silently replaces characters). </summary>
        internal static Encoding GetGb2312() => Encoding.GetEncoding(936, new EncoderExceptionFallback(), new DecoderExceptionFallback());

        static Encoding GetVerifier(string name)
            => Encoding.GetEncoding(name, new EncoderExceptionFallback(), new DecoderExceptionFallback());

        /// <summary>
        /// True when <paramref name="bytes"/> can be decoded fully with <paramref name="verifier"/>.
        /// Files containing NUL bytes are rejected outright — they are UTF-16/UTF-32/binary and must never
        /// be treated as single-byte text (they can strictly "decode" as GB2312 while being nothing of the sort).
        /// </summary>
        static bool CanDecode(byte[] bytes, Encoding verifier, string name)
        {
            if (HasNullBytes(bytes)) return false;
            try
            {
                using (var ms = new MemoryStream(bytes, writable: false))
                using (var reader = new StreamReader(ms, verifier, detectEncodingFromByteOrderMarks: true))
                {
                    reader.ReadToEnd();
                    return string.Equals(reader.CurrentEncoding.BodyName, name, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception e) when (e is DecoderFallbackException
                                   || e is System.Text.EncoderFallbackException
                                   || e is IOException
                                   || e is UnauthorizedAccessException)
            {
                return false;
            }
        }

        static bool HasNonAsciiBytes(byte[] bytes)
        {
            foreach (var b in bytes)
            {
                if (b > 0x7F) return true;
            }
            return false;
        }

        static bool HasNullBytes(byte[] bytes)
        {
            foreach (var b in bytes)
            {
                if (b == 0) return true;
            }
            return false;
        }

        static byte[] ReadAllBytesOrNull(string file)
        {
            try
            {
                return File.ReadAllBytes(file);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary> True when the file can be fully decoded with the given encoding (UTF-8/16 BOM aware). </summary>
        public static bool DetectFileEncoding(string file, string name)
        {
            var bytes = ReadAllBytesOrNull(file);
            return bytes != null && CanDecode(bytes, GetVerifier(name), name);
        }

        /// <summary>
        /// Reads <paramref name="file"/> once and classifies its encoding. Prefer this over chaining the
        /// individual probes — it avoids decoding the same file two or three times per batch/preview.
        /// </summary>
        internal static FileEncodingKind ProbeFileEncoding(string file)
        {
            var bytes = ReadAllBytesOrNull(file);
            if (bytes == null) return FileEncodingKind.Unreadable;
            if (HasNullBytes(bytes)) return FileEncodingKind.Unidentified;
            if (CanDecode(bytes, StrictUtf8, "utf-8"))
            {
                return HasNonAsciiBytes(bytes) ? FileEncodingKind.Utf8 : FileEncodingKind.Ascii;
            }
            if (CanDecode(bytes, StrictGb2312, "gb2312"))
            {
                return FileEncodingKind.Gb2312;
            }
            return HasNonAsciiBytes(bytes) ? FileEncodingKind.Unidentified : FileEncodingKind.Ascii;
        }
    }
}
