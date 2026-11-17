// Copyright (c) Bian Shanghai
// https://github.com/Bian-Sh/Script-Encoding-Converter
// Licensed under the MIT license. See the LICENSE file in the package root for more information.

using System;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace MUD.Modules.ScriptEncodingConverter.Tests
{
    public class ScriptEncodingConverterTests
    {
        string _tempDir;

        [SetUp]
        public void SetUp()
        {
            EnsureCodePagesRegistered();

            _tempDir = Path.Combine(Path.GetTempPath(), "SEC_Tests_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, recursive: true);
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Best effort cleanup; the OS will reap temp folders anyway.
            }
        }

        string WriteTempFile(string fileName, byte[] bytes)
        {
            var path = Path.Combine(_tempDir, fileName);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        static Encoding Gb2312() => Encoding.GetEncoding(936, new EncoderExceptionFallback(), new DecoderExceptionFallback());

        // Register the CodePages provider on runtimes that require it (no-op on Unity Mono).
        static void EnsureCodePagesRegistered()
        {
            try
            {
                var providerType = Type.GetType("System.Text.CodePagesEncodingProvider, System.Text.Encoding.CodePages")
                                ?? Type.GetType("System.Text.CodePagesEncodingProvider, netstandard")
                                ?? Type.GetType("System.Text.CodePagesEncodingProvider, System")
                                ?? Type.GetType("System.Text.CodePagesEncodingProvider, mscorlib");
                if (providerType == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        providerType = asm.GetType("System.Text.CodePagesEncodingProvider", false);
                        if (providerType != null) break;
                    }
                }
                var provider = providerType?
                    .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?
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

        [Test]
        public void AsciiFile_IsDetectedAsUtf8()
        {
            var path = WriteTempFile("ascii.cs", Encoding.UTF8.GetBytes("public class AsciiOnly { }"));
            Assert.IsTrue(EncodingDetector.DetectFileEncoding(path, "utf-8"));
        }

        [Test]
        public void Utf8NoBomWithChinese_IsDetectedAsUtf8()
        {
            var path = WriteTempFile("utf8-nobom.cs", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes("// 写一些中文"));
            Assert.IsTrue(EncodingDetector.DetectFileEncoding(path, "utf-8"));
        }

        [Test]
        public void Utf8WithBom_IsDetectedAsUtf8()
        {
            var preamble = new UTF8Encoding(true).GetPreamble();
            var body = new UTF8Encoding(false).GetBytes("// 写一些中文");
            var bytes = new byte[preamble.Length + body.Length];
            Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
            Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);
            var path = WriteTempFile("utf8-bom.cs", bytes);
            Assert.IsTrue(EncodingDetector.DetectFileEncoding(path, "utf-8"));
        }

        [Test]
        public void Gb2312WithChinese_IsNotUtf8_AndIsGb2312()
        {
            var path = WriteTempFile("gb2312.cs", Gb2312().GetBytes("// 写一些中文"));
            Assert.IsFalse(EncodingDetector.DetectFileEncoding(path, "utf-8"));
            Assert.IsTrue(EncodingDetector.DetectFileEncoding(path, "gb2312"));
        }

        [Test]
        public void EmptyFile_IsDetectedAsUtf8_AndGb2312()
        {
            // Empty files decode fully with any encoding; the caller's "utf-8 first" logic disambiguates.
            var path = WriteTempFile("empty.cs", Array.Empty<byte>());
            Assert.IsTrue(EncodingDetector.DetectFileEncoding(path, "utf-8"));
            Assert.IsTrue(EncodingDetector.DetectFileEncoding(path, "gb2312"));
        }

        [Test]
        public void MissingFile_IsDetectedAsNeither()
        {
            var path = Path.Combine(_tempDir, "does-not-exist.cs");
            Assert.IsFalse(EncodingDetector.DetectFileEncoding(path, "utf-8"));
            Assert.IsFalse(EncodingDetector.DetectFileEncoding(path, "gb2312"));
        }

        [Test]
        public void Gb2312Roundtrip_ConvertsToUtf8()
        {
            var path = WriteTempFile("roundtrip.cs", Gb2312().GetBytes("// 写一些中文"));
            Assert.IsTrue(FileConverter.TryConvert(path, Gb2312(), new UTF8Encoding(false), null, out var error));
            Assert.IsNull(error);
            Assert.AreEqual("// 写一些中文", File.ReadAllText(path, new UTF8Encoding(false)));
            Assert.IsTrue(EncodingDetector.DetectFileEncoding(path, "utf-8"));
        }

        [Test]
        public void UnmappableCharacter_LeavesOriginalBytesUntouched()
        {
            var original = new UTF8Encoding(false).GetBytes("// emoji 😀 test");
            var path = WriteTempFile("emoji.cs", original);
            Assert.IsFalse(FileConverter.TryConvert(path, new UTF8Encoding(false, true), Gb2312(), null, out var error));
            Assert.IsNotEmpty(error);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
        }

        [Test]
        public void UndecodableSource_LeavesOriginalBytesUntouched()
        {
            // The source encoding (strict UTF-8) cannot decode these GB2312 bytes — deterministically
            // across .NET and Mono. (Testing this path with isolated GBK-invalid byte sequences is not
            // portable: Mono's GBK decoder is more lenient than .NET's.)
            var original = Gb2312().GetBytes("// 写一些中文");
            var path = WriteTempFile("undecodable.cs", original);
            var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            Assert.IsFalse(FileConverter.TryConvert(path, strictUtf8, new UTF8Encoding(false), null, out var error));
            Assert.IsNotEmpty(error);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
        }

        [Test]
        public void EmptyFile_ConvertsSuccessfully()
        {
            var path = WriteTempFile("empty-convert.cs", Array.Empty<byte>());
            Assert.IsTrue(FileConverter.TryConvert(path, Gb2312(), new UTF8Encoding(false), null, out var error));
            Assert.IsNull(error);
            Assert.AreEqual(string.Empty, File.ReadAllText(path, new UTF8Encoding(false)));
        }

        [Test]
        public void MissingFile_ConversionReturnsFalse()
        {
            var path = Path.Combine(_tempDir, "missing.cs");
            Assert.IsFalse(FileConverter.TryConvert(path, Gb2312(), new UTF8Encoding(false), null, out var error));
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void Backup_WhenEnabled_CopiesOriginalBeforeConversion()
        {
            var original = Gb2312().GetBytes("// 写一些中文");
            var path = WriteTempFile("backup.cs", original);
            var backupDir = Path.Combine(_tempDir, "backups");
            Assert.IsTrue(FileConverter.TryConvert(path, Gb2312(), new UTF8Encoding(false), backupDir, out var error));

            var mirror = Path.Combine(backupDir, Path.GetFileName(path));
            Assert.IsTrue(File.Exists(mirror));
            Assert.That(File.ReadAllBytes(mirror), Is.EqualTo(original));
            Assert.AreEqual("// 写一些中文", File.ReadAllText(path, new UTF8Encoding(false)));
        }

        [Test]
        public void Probe_ClassifiesAsciiUtf8Gb2312InOneRead()
        {
            var ascii = WriteTempFile("probe-ascii.cs", Encoding.UTF8.GetBytes("public class Plain { }"));
            Assert.AreEqual(FileEncodingKind.Ascii, EncodingDetector.ProbeFileEncoding(ascii));

            var utf8 = WriteTempFile("probe-utf8.cs", new UTF8Encoding(false).GetBytes("// 写一些中文"));
            Assert.AreEqual(FileEncodingKind.Utf8, EncodingDetector.ProbeFileEncoding(utf8));

            var gb2312 = WriteTempFile("probe-gb2312.cs", Gb2312().GetBytes("// 写一些中文"));
            Assert.AreEqual(FileEncodingKind.Gb2312, EncodingDetector.ProbeFileEncoding(gb2312));

            Assert.AreEqual(FileEncodingKind.Unreadable, EncodingDetector.ProbeFileEncoding(Path.Combine(_tempDir, "missing.cs")));
        }

        [Test]
        public void Utf16LeFile_IsNeverMistakenForGb2312()
        {
            // UTF-16LE bytes (ASCII interleaved with NULs + valid-looking GB2312 double-byte pairs)
            // must NOT be classified as convertible text — otherwise the tool would rewrite the file
            // as mojibake while reporting success.
            var path = WriteTempFile("utf16.cs", Encoding.Unicode.GetBytes("// 写一些中文"));
            Assert.IsFalse(EncodingDetector.DetectFileEncoding(path, "utf-8"));
            Assert.IsFalse(EncodingDetector.DetectFileEncoding(path, "gb2312"));
            Assert.AreEqual(FileEncodingKind.Unidentified, EncodingDetector.ProbeFileEncoding(path));
        }

        [Test]
        public void Utf16LeBomFile_IsUnidentified()
        {
            var preamble = new UnicodeEncoding(bigEndian: false, byteOrderMark: true).GetPreamble();
            var body = Encoding.Unicode.GetBytes("// 写一些中文");
            var bytes = new byte[preamble.Length + body.Length];
            Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
            Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);
            var path = WriteTempFile("utf16-bom.cs", bytes);
            Assert.IsFalse(EncodingDetector.DetectFileEncoding(path, "utf-8"));
            Assert.IsFalse(EncodingDetector.DetectFileEncoding(path, "gb2312"));
        }

        [Test]
        public void NormalizeExtensions_NormalizesDeduplicatesAndDropsInvalid()
        {
            var normalized = ScriptEncodingConverterSettings.NormalizeExtensionList(new System.Collections.Generic.List<string>
            {
                " CS ", ".cs", ".CS", "txt", "..md", "file.shader", "bad ext", ".uxml ", null, "", ".already"
            });
            Assert.That(normalized, Is.EqualTo(new[] { ".cs", ".txt", ".md", ".shader", ".uxml", ".already" }));
        }

        [Test]
        public void NormalizeExtensions_NullList_FallsBackToDefaults()
        {
            var normalized = ScriptEncodingConverterSettings.NormalizeExtensionList(null);
            Assert.That(normalized, Is.EqualTo(ScriptEncodingConverterSettings.DefaultSupportedExtensions));
        }

        [Test]
        public void GetMirrorPath_MirrorsRelativePathsUnderBackupRoot()
        {
            var root = Path.Combine(_tempDir, "backup");
            Directory.CreateDirectory(root);
            var mirror = FileConverter.GetMirrorPath(root, "Assets/Scripts/Foo.cs");
            Assert.That(mirror.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase));
            Assert.That(mirror.EndsWith(Path.Combine("Scripts", "Foo.cs")));
        }

        [Test]
        public void GetMirrorPath_AbsolutePath_MirrorsByFileNameOnly()
        {
            var root = Path.Combine(_tempDir, "backup");
            Directory.CreateDirectory(root);
            var asset = Path.Combine(_tempDir, "elsewhere", "Bar.cs");
            var mirror = FileConverter.GetMirrorPath(root, asset);
            Assert.AreEqual(Path.GetFullPath(Path.Combine(root, "Bar.cs")), mirror);
        }

        [Test]
        public void GetMirrorPath_PathEscapingBackupRoot_Throws()
        {
            var root = Path.Combine(_tempDir, "backup");
            Directory.CreateDirectory(root);
            Assert.Throws<ArgumentException>(() => FileConverter.GetMirrorPath(root, "../escape.cs"));
            Assert.Throws<ArgumentException>(() => FileConverter.GetMirrorPath(root, "sub/../../escape.cs"));
        }

        [Test]
        public void BackupFailure_LeavesOriginalFileUntouched()
        {
            var original = Gb2312().GetBytes("// 写一些中文");
            var path = WriteTempFile("backup-fails.cs", original);
            // A regular file where a directory is expected makes directory creation fail deterministically.
            var notADirectory = Path.Combine(_tempDir, "not-a-directory");
            File.WriteAllText(notADirectory, "placeholder");
            Assert.IsFalse(FileConverter.TryConvert(path, Gb2312(), new UTF8Encoding(false), notADirectory, out var error));
            Assert.IsNotEmpty(error);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
        }
    }
}
