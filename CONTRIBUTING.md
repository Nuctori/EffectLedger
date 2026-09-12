# 贡献指引

感谢关注 Cosmos.EffectAlgebra。本项目对「正确性」有较严的工程要求，以下规约请先读再动手。

## 提交前必读

1. **门禁必须绿**：`bash ci.sh`（或 `ci.ps1`）必须 PASS——包含全量构建（`-warnaserror --no-incremental`）、
   **Dafny 形式验证**（89 定律 + verified 计数门）、三测试工程顺序执行。任何一步失败不要提交。
2. **测试只增不减**：不允许删除既有测试或弱化断言。若某测试的对象按设计移除（如 API 收缩），
   须在提交信息与 `audit/qed/ROADMAP.md` 记录理由。
3. **TDD**：可钉住的缺陷先落红灯测试再改实现；语义决策须写入 PDR/README 并附测试证据。
4. **文档与代码同一提交**：禁止「代码先行、文档欠账」。

## 契约冻结警示（最重要）

本库的核心承诺是「一次发布永远不用更新」。以下面被机器钉死，改动即破坏性：

| 面 | 钉 | 改动后果 |
| -- | -- | -------- |
| 公共 API | `QedP1B1PublicApiSnapshotTests` | 任何签名变更 ⇒ 快照红 ⇒ **semver major** |
| JSON 契约 | `QedP4E2SchemaFreezePins` + schema `version` | 契约面变更 ⇒ **semver major** + version 递增 |
| 异常方言 | `QedP0A4ContractFreezePins` | 异常类型变更 ⇒ **semver major** |
| CLI 退出码 | `ProdAuditR3ToolingTests` | 退出码语义变更 ⇒ **semver major** |
| 形式化定律 | `dafny verify` 计数门（≥89 verified） | 删除/弱化定律 ⇒ **CI 红** |

**加功能请优先「新增」而非「修改」**；确需破坏性变更请在 PR 描述中说明 semver 影响。

## 开发环境

- .NET 10 SDK（构建）、.NET 8 SDK（多目标验证）
- Dafny 4.11.0 + Z3 4.12.1（形式验证门；安装见 `.github/workflows/ci.yml` 的步骤）
  - 本地：`dotnet tool install --global Dafny --version 4.11.0`，Z3 置于 `~/.dotnet/tools/z3/bin/`
  - 或经 `DAFNY_Z3` 环境变量指定求解器路径

## 提交规范

Conventional Commits（中文正文，随仓库风格）：

```
fix(scope): 简短描述
feat(scope): 新增能力
docs(scope): 纯文档
chore: 构建/工具链
```

`scope` 建议：`l1`（代数核心）/ `l2`（生成器）/ `l3`（分析器）/ `runtime` / `cli` / `gate`（门禁）/ `docs`。

## 审计文化

本项目经八轮独立对抗审计，审计报告存于 `audit/`。欢迎在 PR 中附「对抗性验证」——
证明你的改动打不穿既有防线，或指出防线漏洞。**发现漏洞同样是贡献**。
