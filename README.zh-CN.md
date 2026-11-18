# MUD Script Encoding Converter（脚本编码转换器）

[English](README.md) | 简体中文

Unity 编辑器扩展：检测并把 GB2312 编码的脚本批量转换为 UTF8——可手动选择、可批量处理，也可以在导入时自动修复。让 UTF-8 / UTF-8 BOM / GB2312 混合的工程不再出现 Inspector 或 Console 里的乱码。

> 重要提示：进行大批量转换前请先用 git 提交你的代码。虽然本工具默认每次转换前都会备份，但版本控制是更可靠的安全网。

## 功能

1. **批量转换** —— 在 Project 窗口选择文件或文件夹，统一转换其下所有受支持的文件（递归）。
2. **Auto Fix（自动修复）** —— 导入/移动支持的文本资产时自动转 UTF-8（**默认关闭**，按项目配置，位于 Project Settings）。
3. **转换前自动备份** —— 每次转换前都先把原文件镜像备份到时间戳目录（**默认开启**）；备份失败则该文件不被覆写。
4. **Preview 菜单** —— 干跑一次：列出会被转换的文件、以及无法识别编码的文件，不写盘。
5. **可点击的控制台报告** —— 报告里的文件路径可点击，Ping 到 Project 窗口中的资产。

## 安装

**通过 git URL（推荐）**

打开 `Window → Package Manager → + → Add package from git URL…`，粘贴：

```
https://github.com/MUDV587/com.mud.modules.script-encoding-converter.git
```

也可以锁定到某个 tag，例如 `…/com.mud.modules.script-encoding-converter.git#v0.1.0`。

需要 Unity **2019.4** 或更高版本。

**通过本地 `file:` 引用**

如果包与工程是兄弟目录存放：

```json
"com.mud.modules.script-encoding-converter": "file:../com.mud.modules.script-encoding-converter"
```

（路径相对于工程 `Packages/` 目录解析）

## 从旧版 Assets 目录迁移

早期版本以「整个 Assets/ScriptEncodingConverter/ 目录拷进工程」的形式分发。迁移步骤：

1. 先 `git commit`（转换/删除文件不可逆，虽然本包所有转换都有备份）。
2. **删除 `Assets/ScriptEncodingConverter/`** 整个目录（包括 `.meta`）；旧目录里若有你自己改过的代码，先手动合并。
3. 按上面的安装方式装包。
4. 重新打开工程并确认：Package Manager 里出现 `Script Encoding Converter`、Console 无报错、菜单只出现一套。

0.1.0 起的行为变化：

- **Auto Fix 改为按项目配置**（不再有 EditorPrefs / 菜单开关）：`Project Settings → MUD → Script Encoding Converter`，默认**关闭**；建议把 `ProjectSettings/ScriptEncodingConverter.asset` 提交到 git。
- **Backup 默认开启**；备份目录留空 = `<temp>/ScriptEncodingConverterBackups/<项目名>/<时间戳>/`。
- 批量转换完成后**一次性 `AssetDatabase.Refresh()`**。

## 配置

`Project Settings > MUD > Script Encoding Converter`（资产存于 `ProjectSettings/ScriptEncodingConverter.asset`，建议提交到 git）：

| 设置 | 默认值 | 说明 |
|---|---|---|
| Auto Fix | 关闭 | 导入/移动时自动转 UTF8 |
| Backup | 开启 | 每次转换前镜像备份；"Open Backup Directory" 按钮可打开备份目录 |
| Backup directory | 空 | 空 = `<temp>/ScriptEncodingConverterBackups/<项目名>`；支持相对/绝对路径 |
| Supported Extensions | 见代码 | **完全接管**：该列表就是手动转换 / Auto Fix / Preview 共用的全部规则 |

## 支持的文件类型

默认：`.cs`、`.shader`、`.cginc`、`.hlsl`、`.compute`、`.asmdef`、`.uxml`、`.uss`、`.txt`、`.md`（大小写不敏感）。选择文件夹会递归处理；列表可在设置页签增删。

> 说明：`To GB2312` 遇到无法用 GB2312 表示的字符（emoji、生僻字）时，该文件会被跳过并在 Console 报警，**不会**被写成 `?`。

## 出处与优化改进（相对上游）

本包是 [Bian-Sh/Script-Encoding-Converter](https://github.com/Bian-Sh/Script-Encoding-Converter)（MIT，原作者 Bian Shanghai / Bian-Sh）的**优化改进版**，上游版权声明保留于 [LICENSE](LICENSE)。本发行版改进要点（完整清单见 [CHANGELOG.md](CHANGELOG.md)）：

- **[修复] UTF-16/UTF-32/二进制文件不再被误判为 GB2312 并重写成乱码**：检测到 `0x00` 字节即判为「无法识别」，所有入口（手动转换 / Auto Fix / Preview）一律跳过并报警。上游中 BOM 以外的 UTF-16LE 中文脚本会严格解码为 GB2312，从而被"成功转换"为乱码。
- **[性能] 单次读盘的 `ProbeFileEncoding`**：手动菜单 / Preview / Auto Fix 失败路径原来对同一文件读 2~3 次盘。
- **[修复] 备份会话目录加毫秒 + 冲突去重**：上游按秒命名，同一秒内的两次批量转换会互相覆盖备份。
- **[修复] `IsReadOnlyPackageAsset` 相对项目根解析**，不再依赖进程工作目录。
- **[修复] Auto Fix 重导入后不再重复入队**（`AssetDatabase.Refresh()` 期间临时抑制回调），且空批次不再创建空备份目录。
- **[修复] `To GB2312` 跳过纯 ASCII / 空文件**（原行为是无意义的原样重写并消耗备份）。
- **[修复] Console 超链接反射钩子（Unity < 2021.1）加保护**，签名不匹配不再每次 domain reload 刷错。
- **[修复] 设置页 `labelWidth` 保存/恢复**，不再污染其他编辑器；`pendingFiles` 去重。
- **[包] 移除 `com.unity.test-framework` 依赖**（测试通过测试 asmdef 引用即可）。
- **[CI] validate 工作流扩展**：包内所有文件的 `.meta` 完整性 + GB2312 演示样本未被 git 重编码的校验。
- **[工程] `Bian.ScriptEncodingConverter` 命名空间、程序集与引用全部重命名为 `MUD.Modules.ScriptEncodingConverter`**，包名 `com.mud.modules.script-encoding-converter`，作者 MUD。

## 运行测试

`Window > General > Test Runner > EditMode`，运行 `MUD.Modules.ScriptEncodingConverter.Tests.Editor` 程序集（检测 + 转换 roundtrip + 备份 + 写盘失败原文件不变 + UTF-16 回归 等用例）。

## 已知局限

- 不含 BOM 的 UTF-8 中文文件，若其字节序列恰好都能被严格 GB2312 解码，则无法与 GB2312 区分，会被跳过。遇到请加 BOM 或手动转换一次。
- 含 NUL 字节的文件（UTF-16/UTF-32/二进制）一律判为无法识别——本工具不做 UTF-16 转码。

## 许可证

MIT License — 见 [LICENSE](LICENSE)。保留上游版权（Bian Shanghai），修改版版权归 MUD。
