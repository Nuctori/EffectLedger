# EffectScript 修复后复核审计（视角 C：真实 bug / fail-open / 代码正确性）

## 审计独立声明

- **独立立场**：本审计为独立对抗性复核，仅依据下发的 8 个源码文件自行推导，**未读取 `audit/` 下任何历史文件**（包括 `effect-script-auditR1.md` / `R2.md` / `R3.md` / `R-synthesis.md` 等）。
- **所读源码（8 个，全量）**：
  - 目标：`EffectScript.cs`、`EffectScriptContract.cs`、`EffectScriptIo.cs`
  - 支撑 L1：`Objects.cs`、`Numeric.cs`、`Algebra.cs`、`SignedNet.cs`、`DerivedMetrics.cs`
  - 注：`EffectScriptIo.cs` 在审计开始时尚存在于磁盘（首次读取成功），编写报告时已被工作区删除（见下「关键环境发现」）。其内容与 `git show HEAD:` 完全一致，审计结论以其内容为据；下文运行时验证改用现存的 `EffectScriptContract` 入口。
- **验证方法（独立执行）**：本机 `dotnet build` 因环境级 NuGet/MSBuild 任务加载失败（`NuGet.Build.Tasks.dll` 无法加载 `Microsoft.Build.Utilities.v4.0`）而全局不可用；因此改用 SDK 自带 `csc.dll`（net10.0 ref pack）将 7 个现存源码 + 驱动程序直接编译为可执行，并用 `dotnet <exe>`（framework-dependent，net10.0 runtime 10.0.9）实跑反例 JSON。本报告中所有「运行时实测」结论均来自该自建可执行，非臆测。

---

## 关键环境发现（影响交付口径，必须先说）

- **`EffectScriptIo.cs` 在工作区被 staged-delete**（`git status` 显示 `D  src/Cosmos.EffectAlgebra/EffectScriptIo.cs`；`Cosmos.EffectAlgebra.csproj` 删除了对应的 `<Compile Remove="EffectScriptIo.cs"/>`，恢复编译；`tests/.../EffectScriptContractTests.cs` 改为调用 `EffectScriptContract.Parse`）。
- 即本次修复把 `EffectScriptIo` 的角色**合并进 `EffectScriptContract`**（删除冗余第二契约解析器）。这是合理的 YAGNI 收敛，但意味着：
  - 任务书点名要求审计的 `EffectScriptIo.cs` 已不再存在 → 该文件符号「良定义」的判定以 HEAD 版本（与我首次读取内容一致）为准，并标注「已被删除/合并」。
  - 原 `EffectScriptIo` 测试已迁至 Contract；若只编译 7 文件，原 `EffectScriptIo` 的 fail-fast 解析路径（更广泛的 resource/scope 形状如 `tree/self/physics/disk/...`）也随之消失——见末尾 OPEN-3。

---

## 逐符号良定义判定表

符号 | 行为 | 良定义? | 反例/证据 | 严重度
---|---|---|---|---
`EffectEvent` (record struct) | 四字段位置记录；`Scope` 随事件携带作为 At/Net/Peak 的 scope 来源 | ✅ 良定义 | 构造即全必填；`Scope` 字段消除旧 OPEN-1 自由变量 | low
`EffectScript.Events` / 构造器 | 有限事件集（ImmutableArray） | ✅ 良定义 | — | low
`EffectScript.At(t)` | 存活事件各取 `Combination.Loop(footprint, loop, Event.Scope)` 后 Union | ⚠️ **scope 来源与 Audit 不一致**（见 OPEN-1） | TC7：`At` 把 claim 重 scope 到 Event.Scope=Global，而 `Audit` gate(3) 按 claim 自带 scope=Scene 分组 → 同一批元素 `At` 显示 Global，`Audit` 报告 Scene 冲突 | **high（语义分裂）**
`EffectScript.Audit(cap)` — gate(1) net | 仅 enter 用 `SignedInterval.Add`（端点求和），非 `Merge` | ✅ **确为 Add（修复已真修）** | TC1：create[0,10] size[2,2] + release[5,15] size[10,10] → 净 `[-8,-8]`；`NegativeDip@t=5,10,15` 与 `Leak@15` 均报出。`Merge` 会得 `[-10,2]`（含 0）漏报 | low（修复验证通过）
`EffectScript.Audit` — gate(1) `NegativeDip` | 采样点 `net.Hi<0` → 报 | ✅ 良定义 | TC1 实测报 `NegativeDip`，`[-8,-8]` | low
`EffectScript.Audit` — gate(1) `Leak`（闭包块） | `closureT` 处按 `Add` 累积有限 ω net，要求 `ContainsZero` | ✅ 良定义（含未来事件排除） | TC8：未来事件 `Lo=100` 被正确排除，closureT=10 无伪 Leak；另 TC8 在 t=110 报未来 create 的 Leak（正确） | low
`EffectScript.Audit` — gate(2) peakSum 进入路径 | `ulong` 乘/加溢出 → 标 `NatStar.Top`，非裸 `ulong*` 回卷 | ✅ **已硬化（修复已真修）** | TC3：两事件 size.hi=ulong.Max，和会回卷；实测 `PeakExceeded`，峰值打印 `?`（即 Top） | low（修复验证通过）
`EffectScript.Audit` — gate(2) peakSum 退出路径 | 对称 `⊥` 兜底 | ✅ 良定义 | 与进入路径同款硬化 | low
`EffectScript.Audit` — gate(3) CompatibleConflict | 按 `(res,scope,mode)` 活跃事件集合 ≥2 报冲突；单事件内 ω 副本不误报 | ✅ 良定义 | TC3 实测 `create×create` 冲突（同 mode 跨事件） | low
`EffectScript.Audit` — 端点采样 | 有限端点排序采样；`Hi=⊤` 采 `maxFinite+1` | ✅ **常驻脚本被采样（修复已真修）** | TC4：`Lo=3,Hi=⊤` 在 t=3,4 报 `PeakExceeded`（预算 5 < 占用 7） | low（修复验证通过）
`EffectScript.Alive` / `ScaleSize` / `ToZ` / `Negate` | 私用辅助 | ✅ 良定义 | 与 `SignedNet.Add` 一致 | low
`Budget` (record struct) | 软约束 caps 表 | ✅ 良定义 | 缺省 None = 无上限 | low
`AuditResult` / `Violation` | 结果载体 | ✅ 良定义 | — | low
`EffectScriptContract.Parse` | JSON→`EffectScript`，fail-fast `FormatException` | ✅ 良定义 | TC2：`memory:42` 正确解析为 `Memory{Uid=42}`；要求每份 claim 自带 scope | low
`EffectScriptContract.ToJson` | `EffectScript`→JSON round-trip | ❌ **丢失 read/write 桶（新 bug，见 OPEN-2）** | TC5：序列化后无 `"write"` 键；反解析 `At` 得 `read=0,write=0,occupy=1`；`SerializeEvent` 仅写 `footprint=OccupyClaims`，忽略 `Read/Write` 桶 | **high（契约不对称）**
`EffectScriptContract` 其余解析器 | interval/scope/loop/claim/resource/budget | ✅ 良定义 | 形状校验齐全 | low
`EffectScriptIo.Parse` (HEAD 版) | 第二个 JSON 解析器，记忆 uid 消费 | ✅ 良定义（已删除/合并） | HEAD 版 `Memory(ExtractUInt64(v,0))` 确消费 uid（非硬编码 0），与注释「修 auditR」一致 | low（但见 OPEN-3）
`EffectScriptIo.ParseResource` 形状覆盖 | 支持 `tree/self/physics/disk/signal/...` 等宽形状 | ⚠️ 合并后该宽覆盖**仅存于已删 Io** | 合并进 Contract 后，Contract `ParseResource` 仅支持 `gpu/commandBuffer/memory/occupancy/signalBus` 5 种；宽资源形状（如 `tree`）在 Contract 下抛 `FormatException` | **medium（能力回退）**

### L1 支撑符号（仅确认被 EffectScript 正确复用，未逐符号重审）

- `Numeric.NatStar` 加/乘/比较的 `⊤` 闭包、环绕检测 → 被 peakSum 硬化路径依赖，正确。
- `Interval` / `SignedNet.SignedInterval.Add` / `Merge` / `ContainsZero` → gate(1) 正确调用 `Add`（非 `Merge`），与 `Algebra.NetTable` 一致。
- `Objects.Claim` / `Signature` 三桶 / `ResourceId.Normalize` → `At` 与 `Audit` 经同一 Normalize 键；但 `At` 经 `Combination.Loop` 把 claim scope 改写为 Event.Scope，导致与 `Audit` gate(3) 的 claim-scope 分组不一致（OPEN-1）。
- `Algebra.Compatible.IsCompatible` → 全函数、16 对覆盖、CONFLICT 集正确；`Unknown→Use` fail-closed。
- `DerivedMetrics.Combination.Loop` / `Scale` → `ω=⊤` 拉上界开放；`At` 复用之。
- `Objects.ScopeId.IncludedIn` → `Global` 最大元、自反；跨标签不可比较返回 false。

---

## 重点验证项（对照已知缺陷）

1. **守恒 gate(1) 用 `Add` 非 `Merge`** —— ✅ 已真修。
   反例 JSON（`tc1`）：
   ```json
   {"events":[
     {"lifetime":[0,10],"scope":{"scene":"S"},"footprint":[{"kind":"occupy","resource":{"memory":1},"mode":"create","scope":{"scene":"S"},"size":[2,2]}]},
     {"lifetime":[5,15],"scope":{"scene":"S"},"footprint":[{"kind":"occupy","resource":{"memory":1},"mode":"release","scope":{"scene":"S"},"size":[10,10]}]}
   ]}
   ```
   实测：净 `[-8,-8]`（Hi=-8<0）→ 报 `NegativeDip@t=5,10,15` 与 `Leak@15`。若用 `Merge`（min/max 包络）得 `[-10,2]`（含 0），会**漏报** NegativeDip 与 Leak。结论：现实现为端点求和，修复有效，无回归。

2. **memory 解析消费 uid** —— ✅ 已真修（以 Contract 为准；Io HEAD 同款）。
   实测 `{"memory":42}` → `Memory{Uid=42}`；round-trip `ToJson` 后 `Parse` 仍得 `Memory{Uid=42}`。非 `Memory(0)`。

3. **peakSum 溢出兜底标 `NatStar.Top`** —— ✅ 已真修。
   实测两事件 size.hi=ulong.Max（loop=1）：进入/退出路径均以 `hi.Value <= ulong.MaxValue / w.Value` 预判 + 加法回卷预判，越界即 `Top`。审计报 `PeakExceeded`，峰值打印 `?`，非裸 ulong* 静默回卷到 0。

4. **纯常驻脚本（Lo>0 且 Hi=⊤）审计** —— ✅ 已覆盖。
   `tc4`：`lifetime:[3,"⊤"]`，size[7,7]，预算 5。采样点含 `maxFinite+1` 且 `Alive` 在 `t∈[3,⊤)` 成立 → 实测 `PeakExceeded@t=3,4`。常驻段未被跳过。

5. **At 与 Audit gate(3) 的 scope 来源是否一致** —— ❌ **不一致（OPEN-1）**。
   `At` 经 `Combination.Loop(footprint, loop, Event.Scope)` 把每个 claim 的 scope 重写为 Event.Scope；`Audit` gate(3) 用 claim 自带 scope 作 `(res,scope,mode)` 分组的键。同一剧本下：`At(t)` 的 occupy claim 全部为 `Event.Scope`（如 Global），而 gate(3) 把冲突归因到 claim 自带 scope（如 Scene"S"）。TC7 实测：`At` 显示 `Global{}`，`Audit` 报 `Scene{Name=S}`。
   - 后果：AI 在 `At` 视角或 `Audit` 视角看到的「冲突作用域」不同，回修 JSON 时作用域线索错位；且若 Event.Scope 与 claim scope 故意不同（分层 scope），二者数学对象不是同一签名。
   - 这不是崩溃（fail-closed 仍报冲突），但属于**语义分裂**，需在契约文档中明示且应统一为同一 scope 来源。

6. **ToJson 是否丢 read/write 桶** —— ❌ **丢（OPEN-2，新 bug）**。
   `SerializeEvent` 仅序列化 `OccupyClaims`，忽略 `ReadClaims`/`WriteClaims`。任意含 read/write 的剧本经 `ToJson`→`Parse` 后 `At` 只剩 occupy 桶。TC5 实测：原 `At` 为 `read=1,write=1,occupy=1`；round-trip 后 `read=0,write=0,occupy=1`。这是契约**不对称**——`Parse` 能读 read/write，`ToJson` 不能写，破坏 round-trip 良定义。

---

## 仍 OPEN 项（按严重度）

- **OPEN-1 (HIGH)** — `EffectScript.At` 与 `EffectScript.Audit` gate(3) 使用不同 scope 来源（Event.Scope vs Claim.Scope），同一剧本两视角语义分裂。证据 TC7。建议：要么 `At` 也用 claim 自带 scope（不经 `Combination.Loop` 改写），要么在 `EFFECT_SCRIPT.md` 明确「`At` 重 scope 到 Event.Scope，`Audit` gate(3) 用 claim scope」并让 AI 据此理解冲突归因。
- **OPEN-2 (HIGH)** — `EffectScriptContract.ToJson` 丢失 read/write 桶，破坏 round-trip 良定义。证据 TC5。修复点：`SerializeEvent` 须序列化 `e.Footprint.ReadClaims` 与 `WriteClaims` 三桶（现仅 `OccupyClaims`）；`SerializeClaim` 已通用，仅需在事件中合并三桶枚举。
- **OPEN-3 (MEDIUM)** — 合并 `EffectScriptIo` 进 `EffectScriptContract` 后，原 Io 的宽 resource/scope 形状（`tree/self/physics/disk/signal/audioMixer/callback/network/input/custom` 等）在 Contract 下不再被支持，仅支持 5 种。若已有剧本/测试依赖这些形状，解析会抛 `FormatException`。需确认是否为有意为之（YAGNI 收敛）；若需保留，应在 Contract 补全或在删除前迁移。
- **OPEN-4 (LOW / 非阻断)** — `EffectScriptContract.Parse` 要求每份 claim **自带 scope**，而 `EffectScriptIo`（已删）用 event scope 兜底；合并后若遗留「claim 无 scope」的旧 JSON，会 fail-fast。属行为变更，应在契约变更说明中标注。

---

## 总评

- 本轮重点修复的 4 项（gate(1) 用 `Add` 非 `Merge`、memory 消费 uid、peakSum 溢出标 `⊤`、常驻脚本被采样）**均已真修无回归**，并由独立编译实跑的反例 JSON 证实：
  - gate(1) `Add`：TC1 报出 `NegativeDip`+`Leak`（Merge 会漏）。
  - memory uid：TC2 解析为 `Memory{Uid=42}`。
  - peakSum 溢出：TC3 报 `PeakExceeded`（峰值 `?`=Top）。
  - 常驻采样：TC4 在 t=3,4 报 `PeakExceeded`。
- **不能声明「全部符号良性定义」**：存在 2 个 HIGH 级 open 项（OPEN-1 scope 分裂、OPEN-2 ToJson 丢桶），会使 `At`/`Audit` 互证失败与 `ToJson` round-trip 破坏。
- 环境级构建不可用（NuGet 任务加载失败），已用 `csc.dll` 直编 7 源 + 驱动实测绕过，结论可独立复现。
- `EffectScriptIo.cs` 已在工作中被删除并合并入 Contract；本报告以其 HEAD 版本为据判定其历史符号已良定义，但该能力在合并后存在回退（OPEN-3）。

## 最该做的三件事

1. **修 OPEN-2**：`EffectScriptContract.SerializeEvent` 输出 read/write/occupy 三桶，恢复 `ToJson` round-trip 良定义（一行枚举合并即可，零新代数结构）。
2. **修 OPEN-1**：统一 `At` 与 `Audit` gate(3) 的 scope 来源，或在 `EFFECT_SCRIPT.md` 显式标注两者 scope 语义差异，避免 AI 回修错位。
3. **确认 OPEN-3 意图**：明确 `EffectScriptIo` 删除是否为终态；若是，把其支持的宽 resource/scope 形状在 `EffectScriptContract` 补齐或显式声明不再支持，避免遗留 JSON 静默 fail-fast。

## 最该砍的三件事（YAGNI）

1. 已删的 `EffectScriptIo.cs` 冗余第二解析器——删除正确，无需恢复。
2. `Audit` 中 `grp` 用 `(ResourceId, ScopeId, int)` 三元组 + `HashSet<int>` 维护活跃集，内存 O(E·K)，对超大剧本可接受；但若确认无跨事件同 mode 多副本场景，可简化为「计数」而非集合（仅当 confluence 无需还原事件 id 时）。属微优化，非必须砍。
3. `SerializeClaim`/`ParseClaim` 中 `size` 默认 `Interval.Default=[1,1]`，零 size 显式 `[0,0]` 与缺省区分已处理，无冗余抽象可砍。

---

## 验证证据（独立执行）

- 编译：`dotnet <sdk>/Roslyn/bincore/csc.dll -target:exe -nullable -langversion:latest -r <net10.0-ref>/*.dll Objects.cs Numeric.cs SignedNet.cs Algebra.cs DerivedMetrics.cs EffectScript.cs EffectScriptContract.cs Driver.cs` → 0 错误。
- 运行：`dotnet Driver.exe` 输出（节选，编码乱码为中文 GBK 控制台，结论以符号为准）：
  - TC1：Passed=False，viols=4 → `NegativeDip@t=5/10/15`、`Leak@15`，net `[-8,-8]`。✅ 证 Add。
  - TC2：`Memory{Uid=42}`，round-trip 仍 `Memory{Uid=42}`。✅ 证 uid 消费。
  - TC3：Passed=False，viols=7 → 含 `PeakExceeded Memory{Uid=1} 峰值 ? > 预算 100`。✅ 证溢出标 Top。
  - TC4：Passed=False → `PeakExceeded@t=3,4`。✅ 证常驻被采样。
  - TC5：`At read=1,write=1,occupy=1`；`ToJson.Contains("write")=False`；round-trip `read=0,write=0,occupy=1`。❌ 证 OPEN-2。
  - TC7：`At` 显示 `Global{}`；`Audit` 报告 `Scene{Name=S}` 冲突。❌ 证 OPEN-1。
  - TC8：未来事件 `Lo=100` 未在 closureT=10 产生伪 Leak；正确在 t=110 报未来 create 的 Leak。✅ 证闭包未来事件排除。
- `git` 状态旁证：`D  src/Cosmos.EffectAlgebra/EffectScriptIo.cs`、`M  csproj`（移除 Compile Remove）、`M  tests/.../EffectScriptContractTests.cs`（改调 Contract）确认合并意图。
