# PUBLISH-CHECKLIST — 发布就绪清单（QED 终态，E4）

> 本清单就绪即 QED 路线收官。**发布动作（5–8）永远由人类执行**——自动化严禁发布（QED 使命条款）。
> 每次发布前从头过一遍本清单；任何一步失败 ⇒ 停止，修复后重来。

## 1. 版本决策（人类定夺）

- [ ] 确定首发版本号：`1.0.0`（正式）或 `0.x.0`（实验期，向用户声明不稳定）。
- [ ] 修改 `Directory.Build.props` 中唯一一行 `<Version>`（semver：破坏性 +.1、功能 +.1、修复 +.0.1）。
  - 全部 5 包（L1/Analyzer/Generator/Runtime/Tool）随单一真源统一变化（钉 `QedP4E1VersioningPins`）。
  - **必须递增版本**：同 id+version 重复发布会因 NuGet 缓存/不可变政策失败（README ⓪ 注记的根除机制）。

## 2. 发布前置门（全部必须绿）

- [ ] `bash ci.sh`（或 `ci.ps1`）PASS：
  - 全量构建 0 错误（`-warnaserror --no-incremental`）；
  - **Dafny 形式验证 89 定律 0 errors**（formal/*.dfy，P3 证明产物）；
  - 测试全绿（≥705：L1 512 + Runtime 120 + SampleGame 73，只增不减）。
- [ ] `dotnet pack -c Release` 五包走查成功（五 nupkg 含 Tool）。
- [ ] GitHub Actions CI（push/PR 门）最近一次运行绿：`gh run list --limit 1`。

## 3. 契约冻结核对（发布即永久，逐条确认）

- [ ] 公共 API 快照 = 契约面（43 类型，钉 `QedP1B1PublicApiSnapshotTests`）——本次发布不再有未审阅面变化。
- [ ] JSON 契约面冻结（6 资源 × 4 scope × kind 3 × mode 5，schema `version` 与 Directory.Build.props 一致，钉 `QedP4E2SchemaFreezePins`）。
- [ ] 语义冻结项在位：异常方言表（#17）、CLI 退出码（README ⑤）、Unknown 三维契约（#1）、双 ⊤ 两轴（#2 前身 A1）。
- [ ] README「诚实边界」20 条已过 E3 复核（6 已解决划线 + 14 活跃附证据）。

## 4. 发布物核对

- [ ] 五个 nupkg：`Cosmos.EffectAlgebra` / `.Analyzer` / `.Generator` / `.Runtime` / `.Tool`。
- [ ] 依赖闭包正确：单装 Generator 自动联装 L1（钉 R2B-01/「干净缓存实验」）；L1 三 TFM（net8.0/net10.0）齐。
- [ ] 包内 README 存在（NU5039 门）；RepositoryUrl 指向本仓库。

## 5. 发布（人类执行）

- [ ] `dotnet nuget push <pkg> --api-key <KEY> --source https://api.nuget.org/v3/index.json`（逐包，Tool 最后）。
- [ ] nuget.org 页面核对：版本、依赖组、README 渲染、许可证（MIT）。

## 6. GitHub Release（人类执行）

- [ ] 打 tag `v<版本>` 并推送。
- [ ] Release 说明：从 `audit/qed/ROADMAP.md` 勾选记录与本日提交历史提炼（含 89 定律清单与诚实边界快照）。

## 7. 发布后同步

- [ ] README ⓪「发布状态（R3-DT-01）」注记删除/改写（`dotnet add package` 从此可用）。
- [ ] `CHANGELOG.md` 补本版本条目（验收员 N7：发布后清单须含 CHANGELOG 步骤）。
- [ ] `templates/README.md` 安装命令去「源码引用」分支。
- [ ] 主 README「测试与仪表航迹」补一行：`dafny verify` 形式验证门（89 定律）。

## 8. 维护模式（发布即启用）

- 每晚对抗模糊测试（随机剧本 fuzz + 变异门 + 性能曲线钉），回归即修。
- 任何公共面变更（QedP1B1 快照红）/ 契约面变更（QedP4E2 红）/ 定律失败（dafny 红）
  ⇒ 必须 semver major + PDR 决策记录——这是「一次发布永远不用更新」承诺的执行面。
