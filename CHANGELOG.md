# Changelog

All notable changes to this package are documented in this file.

## [0.1.0] - 2026-11-17 — MUD 首发版 / MUD first release

本包是 [MUDV587/Script-Encoding-Converter](https://github.com/MUDV587/Script-Encoding-Converter)（原作者 **Bian Shanghai / Bian-Sh**，MIT）的派生版本，版本号沿用上游 `0.1.0`。相比上游 v0.1.0，本版的变化（in English below）：

### Added
- Editor tests: UTF-16LE / UTF-16-BOM files are never mistaken for GB2312 (regression for the NUL-byte guard), `ProbeFileEncoding` classification, `NormalizeExtensionList` normalization/dedup/fallback, `GetMirrorPath` mirroring + path-escape guard, backup-failure leaves the original file untouched.
- Validate workflow: packages' `.meta` completeness now covers all files (not only `.cs`), and a new check fails the build if the GB2312 demo sample ever gets re-encoded to UTF-8.

### Fixed
- Files containing NUL bytes (UTF-16/UTF-32/binary) are now classified as **Unidentified** everywhere and are never converted — previously a BOM-less UTF-16LE file with Chinese passed the strict GB2312 decode and was rewritten as "GB2312 → UTF-8" mojibake while reporting success.
- Backup session directories now include milliseconds and de-duplicate on collision, so two batches started within the same second no longer overwrite each other's backups.
- `IsReadOnlyPackageAsset` resolves relative paths against the project root instead of the process working directory.
- Auto Fix re-import after a successful conversion no longer re-queues every converted file (suppress flag around `AssetDatabase.Refresh()`).
- Auto Fix no longer creates an empty backup session directory when no file needs conversion (created lazily, like the manual path).
- `To GB2312` no longer rewrites pure-ASCII files (they are already valid GB2312 — the rewrite was a byte-identical no-op that still consumed a backup slot and reported activity).
- Console hyperlink reflection hook (Unity < 2021.1) is wrapped so a signature mismatch can no longer spam the Console on every domain reload.
- Settings page no longer leaks `EditorGUIUtility.labelWidth = 220f` into other editors.
- `pendingFiles` in the Auto Fix queue is de-duplicated (imported + moved the same file twice).

### Changed
- **重命名 / Renamed**: package `com.biansh.script-encoding-converter` → `com.mud.modules.script-encoding-converter`; assemblies/namespaces `Bian.ScriptEncodingConverter` → `MUD.Modules.ScriptEncodingConverter`; author is now **MUD**; upstream copyright retained in `LICENSE`.
- Encoding detection now has a single-read `ProbeFileEncoding` (UTF-8/GB2312/Unidentified via one file read) used by the manual menus, Preview and Auto Fix — previously failing files were read two times.
- `package.json` no longer depends on `com.unity.test-framework` (tests reference the test framework through the test asmdef; the dependency forced a version on consumer projects).
- `repository` / `documentationUrl` / `changelogUrl` were removed from `package.json`; they will be filled in once this repository is published on GitHub.
- The editor log reports are unchanged in format; only the detection plumbing changed.

---

## 上游历史 / Upstream history (v0.1.0 by Bian Shanghai / Bian-Sh)

### Added
- UPM package layout: `package.json`, editor asmdef (`Bian.ScriptEncodingConverter.Editor`), namespace `Bian.ScriptEncodingConverter`.
- **Project Settings** page (`Project Settings > Script Encoding Converter`), persisted in `ProjectSettings/ScriptEncodingConverter.asset`:
  - `Auto Fix` (default **off**, per project, meant to be committed) — replaces the old EditorPrefs/Assets-menu toggle.
  - `Supported Extensions` (full takeover) — seeds the default list (`.cs`, `.shader`, `.cginc`, `.hlsl`, `.compute`, `.asmdef`, `.uxml`, `.uss`, `.txt`, `.md`) on first access; the list is used as-is by manual conversion, Auto Fix and the Preview menu.
  - `Backup` (default **on**) with a configurable directory (empty = `<temp>/ScriptEncodingConverterBackups/<project name>`), `Browse…` picker and an `Open Backup Directory` button; relative paths are resolved from the project root.
- **Preview** menu — dry-run that lists files that would be converted and files whose encoding cannot be identified. Neither writes files nor re-imports.
- **Batch backup** before any conversion; a file is skipped with a warning when its backup fails — files are never overwritten without a backup when backup is enabled.
- GitHub Actions `validate` workflow (required files, JSON/asmdef validation, `.cs` ↔ `.meta` completeness, package text files must be valid UTF-8) and `release` workflow (tag push or manual dispatch → version bump + tag + GitHub Release).
- Editor tests (NUnit, `Tests/Editor/`): detection (ASCII / UTF-8 / UTF-8 BOM / GB2312 / empty / missing) + conversion roundtrip + backup-before-write + "unmappable characters never truncate the source file".
- `CHANGELOG.md`.

### Fixed
- Clickable Console hyperlinks now Ping the `.cs` asset (loaded as `UnityEngine.Object` instead of `TextAsset`, which always returned null).
- `isConvertManually` is reset in a `finally` block, so Auto Fix can no longer get permanently stuck off after an exception.
- Conversion encodes fully in memory **before** touching the disk — characters with no target-encoding representation (e.g. emoji in `To GB2312`) now abort the file cleanly instead of leaving a truncated file behind.
- Backup failure prevents overwriting (previously a failed conversion could leave a half-written file).
- `ReadAllText`/`WriteAllText` roundtrips now use strict exception-fallback encodings end-to-end.
- Batch conversion re-imports once via `AssetDatabase.Refresh()` instead of per-file `ImportAsset`.
- Auto Fix: `isConvertManually` guards the import callback; conversion happens on `EditorApplication.delayCall` after the import completes (never inside the import callback).
- `isNeedConvertToUtf8` reads the file once instead of three times; byte-level probes for non-ASCII/GB2312 validity.
- Console reports are printed only when there is something to report; the no-op "No encoding issues found" message is gone.
- `EditorPrefs` keys (`SEC_AutoFix`, `Bian.ScriptEncodingConverter.AutoFix`) are gone; the Auto Fix toggle now lives in Project Settings.
- Detection catch blocks are narrowed to expected exceptions (decoder/encoder fallback, IO, access) instead of swallowing everything.
- Reports cap listed paths at 50 (overflow as "...and N more"); unidentified-encoding files are reported as `Cannot identify encoding` **warnings** (manual `To UTF8` + Auto Fix + Preview); hyperlinks work for both converted and failed/unidentified files.
- `Library/` (incl. `PackageCache`) detection matches the project root precisely (no false positives on e.g. `~/Library/…` on macOS).
- `.cs` filtering and all extension checks are case-insensitive.

### Changed
- Directory layout follows UPM conventions: sources under `Editor/`, split by responsibility (`ScriptEncodingConverter.cs` entry menus, `EncodingDetector.cs`, `FileConverter.cs`, `ScriptEncodingConverterSettings.cs`, `ScriptEncodingAutoFixHandler.cs`, `ConsoleLink.cs`).
- `Auto Fix` is per project, configured in Project Settings (asset is commit-friendly).
- Console log output is English-only.
- Code is split into one file per class; comments are English.
