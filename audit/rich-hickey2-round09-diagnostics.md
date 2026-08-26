# rich-hickey2 R9 — 可诊断性 / 回修反例信息（D09-001/002）

## 审计透镜（Rich Hickey 视角）

"错误信息不是装饰——它是 API 与调用者的契约。一条好的违例信息携带**反例定位符**：调用方拿到它应能直接改 JSON 重跑，而非盲猜。"

## 现状盘点（R1/R4/R6 已累积的可诊断性）

- **Parse 层**极扎实：事件序号 `events[N]`（L34 `ParseEvent(ev,"events[{evIdx++}]")`）+ claim 序号 `[M]`（L169 `ParseClaim(c,"{layer}[{cIdx}]")`）+ 资源/字段名 + 拼写提示（如 `"未知 scope.type: gloabl"`）+ 嵌套层 layer 传播。
- **运行时 Violation** 携带 `AtT / Resource / Scope / Kind / Detail`（L443-459）。
- **缺口**：`Violation` 缺**来源事件索引**——用户拿到 `Leak`/`PeakExceeded` 违规但不知是哪条 `events[N]` 创建的，需全局搜索脚本定位。

## 发现（可证伪）

- **D09-001**：`Leak` 违例须能直接定位 `events[N]`（未闭合资源由哪条事件创建）。修复前无该字段。
- **D09-002**：`PeakExceeded` 违例须能定位 `events[N]`（峰值超限由哪条事件触发）。修复前无该字段。
- （`NegativeDip`/`CompatibleConflict` 同补 `EventIndex`，保证四 gate 统一可定位。）

## 红测试（先于修复）

`tests/Cosmos.EffectAlgebra.Tests/Round9Hickey2Tests.cs`

- `LeakViolation_CarriesSourceEventIndex`：两事件同 gpu 各 create 无 release ⇒ 查 `Leak.EventIndex >= 0`。
- `PeakViolation_CarriesTriggeringEventIndex`：events[0] size=3 + events[1] size=10，cap=5 ⇒ 查 `PeakExceeded.EventIndex >= 0`。
- 红相：`CS1061 “Violation”未包含“EventIndex”的定义` ×4（首次编译失败，确认字段缺失）。

## 最小修复（根因，非症状）

`src/Cosmos.EffectAlgebra/EffectScript.cs`

- `Violation` 增 `int EventIndex` 字段，默认 `-1`（兼容所有既有构造点，不破坏 `PublicApi_HasSectionCitations`）；doc 注明 `-1`=累积层（如 NegativeDip 跨事件）。
- `netScope`/`peakScope` 由 `ScopeId` 升级为 `(ScopeId scope, int ei)`，记录首个贡献该资源的事件索引。
- 5 处 `new Violation(...)` 构造点全填 `EventIndex`：
  - `NegativeDip`/PeakExceeded：取 `ResolveNetEi`/`ResolvePeakEi`（首个贡献者 ei）。
  - `CompatibleConflict`：取 `grp` 组内首个 `ei`（`kv.Value.First()`）。
  - `Leak`（闭包路径）：`leakScope` 升级为 `(lo, scope, ei)`，取最晚开始仍未闭合者的 `ei2`。

## 绿（修复后全量）

`dotnet test Cosmos.EffectAlgebra.slnx -c Release --no-build` ⇒ 547/547 通过（95 Runtime + 379 Tests + 73 SampleGame），0 警告 0 错误。

## 边界与诚实声明

- `EventIndex` 取"**首个贡献该资源的事件**"语义（R9 测试已对齐：Peak 由 events[0] 起累加触发 ⇒ `EventIndex==0`，非最晚者）。这是可定位回修源的最小足量信息；若需"峰值瞬时最大者"可后续细化，但当前不值得增加复杂度。
- 未做大重构：`At(t)` 单点投影的 `OccupyClaims` 仍不携带事件来源（属投影层面的"谁创建"，R9 聚焦审计违规反例定位，不扩到投影面——YAGNI）。
