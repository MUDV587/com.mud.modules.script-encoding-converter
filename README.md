# MUD Script Encoding Converter

English | [简体中文](README.zh-CN.md)

Unity Editor extension that detects and converts GB2312-encoded scripts to UTF8 — on selection, in bulk, or automatically on import. Keep UTF-8, UTF-8-BOM and GB2312 files in one project without mojibake in the Inspector or Console.

> Important: before running a bulk conversion, commit your scripts to git. Bulk operations always make a backup first (enabled by default), but your safety net should still be your own VCS.

## Features

1. **Batch conversion** — select files or folders in the Project window and convert everything supported underneath, recursively.
2. **Auto Fix** — automatically converts supported text assets when they are imported or moved (**off by default**, configured per project in Project Settings).
3. **Backup before write** — every conversion mirrors the original file into a timestamped backup directory first (**on by default**). If the backup fails, the file is left untouched.
4. **Preview menu** — dry-run the batch: lists the files that would be converted and the ones whose encoding cannot be identified. Nothing is written.
5. **Clickable reports** — click a file path in the Console to ping the asset in the Project window.

## Installation

**Via git URL (recommended)**

Open `Window → Package Manager → + → Add package from git URL…` and paste:

```
https://github.com/MUDV587/com.mud.modules.script-encoding-converter.git
```

Pin to a tag if you prefer reproducible installs: `…/com.mud.modules.script-encoding-converter.git#v0.1.0`.

Requires Unity **2019.4** or newer.

**Via local `file:` reference**

If you keep the package checked out next to your project:

```json
"com.mud.modules.script-encoding-converter": "file:../com.mud.modules.script-encoding-converter"
```

(relative to the `Packages/` folder of the consuming project)

## Migrating from the old Assets layout

The pre-UPM versions shipped as "copy `Assets/ScriptEncodingConverter/` into your project". To migrate:

1. `git commit` first (bulk edits are not undoable without git, though this package always backs up before writing).
2. **Delete `Assets/ScriptEncodingConverter/`** entirely (including `.meta` files). If you modified it locally, merge your changes first.
3. Install the package as shown above.
4. On next launch confirm that Package Manager lists `Script Encoding Converter` and the console is clean; the `Assets > Script Encoding Converter` menus should appear exactly once.

Behavioural notes since 0.1.0:

- **Auto Fix is per project** (no more EditorPrefs/assets-menu toggle): `Project Settings → MUD → Script Encoding Converter`, default **off**; commit `ProjectSettings/ScriptEncodingConverter.asset` to share with your team.
- **Backup is on by default**; empty Backup directory means `<temp>/ScriptEncodingConverterBackups/<project name>/<timestamp>`.
- Batch conversions re-import once at the end (`AssetDatabase.Refresh()`).

## Configuration

`Project Settings > MUD > Script Encoding Converter` (asset stored at `ProjectSettings/ScriptEncodingConverter.asset`, commit it to git):

| Setting | Default | Description |
|---|---|---|
| Auto Fix | off | Auto-convert supported text assets on import / move |
| Backup | on | Mirror original files before every conversion; enable "Open Backup Directory" in Settings to browse |
| Backup directory | empty | Empty = `<temp>/ScriptEncodingConverterBackups/<project name>`; relative or absolute paths accepted |
| Supported Extensions | see code | **Full takeover**: the list is the complete rule set used by manual conversion, Auto Fix and Preview |

## Supported file types

Default: `.cs`, `.shader`, `.cginc`, `.hlsl`, `.compute`, `.asmdef`, `.uxml`, `.uss`, `.txt`, `.md` (case-insensitive). Selecting a folder recurses. Edit the list in the Settings page.

> `To GB2312` skips any file whose content cannot be represented in GB2312 (emoji, rare CJK characters) with a warning instead of writing `?` replacements.

## Attribution & improvements over upstream

This package is an **improved derivative** of [Bian-Sh/Script-Encoding-Converter](https://github.com/Bian-Sh/Script-Encoding-Converter) (MIT, original author Bian Shanghai / Bian-Sh). Upstream copyright is retained in [LICENSE](LICENSE). Highlights of this distribution (see [CHANGELOG.md](CHANGELOG.md) for the full list):

- **UTF-16/UTF-32/binary files are never mistaken for GB2312 anymore** (NUL-byte guard) — they used to be "successfully converted" into mojibake.
- **One-read encoding probe** (`ProbeFileEncoding`) shared by menus, Preview and Auto Fix (previously the failing paths read the file twice).
- **Backup sessions are timestamped to the millisecond and de-duplicated** — two batches in the same second can no longer overwrite each other's backups.
- **`To GB2312` skips pure-ASCII / empty files** (they were byte-identical no-ops that still consumed backups and reported activity).
- **Auto Fix no longer re-queues its own re-import**, and empty batches no longer leave empty backup directories behind.
- **`IsReadOnlyPackageAsset` resolves relative to the project root**, not the process working directory.
- **Package hygiene**: `com.unity.test-framework` dependency removed (tests use the test asmdef), `.meta` completeness enforced for all package files in CI, and the GB2312 demo sample is guarded against accidental re-encoding by `.gitattributes` + CI.
- Assemblies and namespaces renamed to `MUD.Modules.ScriptEncodingConverter`; package id `com.mud.modules.script-encoding-converter`.

## Running the tests

Open `Window > General > Test Runner`, select **EditMode** and run the `MUD.Modules.ScriptEncodingConverter.Tests.Editor` assembly (detection, roundtrip, backup-before-write, UTF-16 regression and more).

## Limitations

- A BOM-less UTF-8 file whose Chinese bytes all look like valid GB2312 is indistinguishable from GB2312 and will be skipped. Add a BOM once, or convert it manually.
- Files containing NUL bytes (UTF-16/UTF-32/binary) are always reported as unidentified — this tool does not transcode UTF-16.

## License

MIT License — see [LICENSE](LICENSE). Upstream copyright (Bian Shanghai) is retained; modifications are by MUD.
