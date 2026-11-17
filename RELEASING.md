# Releasing / 发布流程

发布由 `.github/workflows/release.yml` 完成，两种方式任选其一。

## 方式一：手动打 tag（推荐）

1. 在仓库根的 `package.json` 更新 `version`（如 `0.1.0`）
2. 在 `CHANGELOG.md` 添加 `## [0.1.0] - yyyy-MM-dd` 小节
3. 提交并推送到 `main`
4. 打 tag 并推送：
   ```
   git tag v0.1.0
   git push origin v0.1.0
   ```
5. GitHub Actions 自动：校验 tag 与 `package.json` 版本一致、CHANGELOG 包含该版本、创建 GitHub Release（自动生成 notes）

## 方式二：workflow_dispatch 自动 tag

1. 先手动把 `CHANGELOG.md` 的新版本小节提交到 `main`（工作流不改 CHANGELOG）
2. 到仓库 **Actions > release > Run workflow**，输入版本号（如 `0.2.0`，不带 `v`）
3. 工作流自动：校验 CHANGELOG、更新 `package.json` 的 `version`、提交到 `main`、创建 tag、创建 GitHub Release

注意：方式二需要 `main` 分支允许 GitHub Actions 推送（没有分支保护，或 bot 在允许名单内）。若仓库开了分支保护，请用方式一。

## 发布前准备 / Before the first release

远端仓库地址尚未配置。发布前请：

1. 在 GitHub 创建空仓库（例如 `MUD/com.mud.modules.script-encoding-converter`）；
2. 在 `package.json` 填回 `repository` / `documentationUrl` / `changelogUrl`（参考 README 安装章节）；
3. 提交并推送：
   ```
   git remote add origin https://github.com/<owner>/com.mud.modules.script-encoding-converter.git
   git push -u origin main
   ```
4. 之后每次发版重复方式一/方式二即可。

## Release 之后

用户在项目 `manifest.json` 中这样安装带版本号的包（包内容即为仓库根，无需 `?path=`）：

```json
"com.mud.modules.script-encoding-converter": "https://github.com/<owner>/com.mud.modules.script-encoding-converter.git#v0.1.0"
```

## validate 工作流

每次 push / PR 到 `main` 时运行 `.github/workflows/validate.yml`，检查：

- 包必需文件（`package.json` / `README.md` / `CHANGELOG.md` / `LICENSE`）存在
- 所有 JSON / asmdef 可解析，`package.json` 必填字段与语义化版本号正确
- 包内（Unity 忽略的 `~` 目录、点目录/点文件、`*.meta` 自身除外）所有文件都有对应的 `.meta`，以及 GB2312 演示样本未被 git 重编码的完整性校验
- 包内文本文件均为合法 UTF-8
- `CHANGELOG.md` 包含当前版本的小节
