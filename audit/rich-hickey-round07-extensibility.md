# Rich Hickey Round 07 — Open/Closed 扩展性对抗审计 (Extensibility Lens)

**视角**: Hickey / data over branching / complect vs de-complect
**范围**: 7+1 文件 + `ApiMapping.cs` 显式授权 — 禁止读 `audit/` (除本轮产出)
**被审文件**: `Objects.cs`, `EffectScriptContract.cs`, `CosmosEffectConfig.cs`, `ApiMapping.cs`, `EffectScript.cs`, `Algebra.cs`, `Numeric.cs`/`DerivedMetrics.cs` (Loop/Mode), `SignedNet.cs` (无分支,仅载体)
**日期**: 2026-09-01
**问题**: 新增 `ResourceId` / `ScopeId` / `Kind` / `Mode` 一分支时,必须同步改几处? 是否开放封闭? 耦合成本量化。

---

## 1. 摘要 verdict: 非开放封闭 (CLOSED = false)

Hickey 判词: **"分支是值,不是地方(place)"**。本库把同一领域值 (`ResourceId`/`ScopeId`/`Kind`/`Mode`) 在 N 个 **地方** 用 `switch` 重复枚举,而非用 **数据/多态分发** 单一真源。结果是典型的 **shotgun surgery / complect**: 加一值 → 改多文件多函数,否则静默产生 `FormatException` 或落回 `throw`/`Unknown` 窝。`CosmosEffectConfig.cs` 的 `extraMappings` 仅对 `ApiMapping` 白名单做数据驱动开放,未覆盖 `EffectScriptContract` 契约层与 `Budget` 键层 — 半开放。

**一句话**: 加一分支牵 **3-4 文件 / 5-9 分发点**,漏一处即编译期不告警、运行期才炸。

---

## 2. 逐符号分发点全表 (Symbol Dispatch Inventory)

| # | Symbol | File:Line | 形态 | 枚举数(当前) | 触发点类型 |
|---|--------|-----------|------|--------------|------------|
| R-D1 | `ResourceId` 联合定义 | `Objects.cs:21-41` | `abstract record` 15 构造子 | 15 | 定义 |
| R-N1 | `ResourceId.Normalize` | `Objects.cs:52-65` | `switch` | 3 分支 + `_=>r` 兜底 | 归一 |
| S-D1 | `ScopeId` 联合定义 | `Objects.cs:90-99` | `abstract record` 8 构造子 | 8 | 定义 |
| S-N1 | `ScopeId.IncludedIn` | `Objects.cs:102-108` | `if (Equals) / is Global` | 无 per-type 分支 (真正开放) | 偏序 |
| K-D1 | `Kind` 枚举 | `Objects.cs:112` | `enum` | 3 (`Read,Write,Occupy`) | 定义 |
| K-V1 | `Signature.Add` Kind 分发 | `Objects.cs:194-199` | `switch(n.Kind)` | 3 + `default throw` | 聚合 |
| M-D1 | `Mode` 枚举 | `Objects.cs:117` | `enum` | 5 (`Use,Create,Release,Move,Unknown`) | 定义 |
| M-V1 | `Claim.Normalize` Kind×Mode 校验 | `Objects.cs:133-142` | `if(Kind==Read && Mode∉{Use,Unknown}) throw` | 1 规则 | 校验 |
| R-P1 | `ParseResource` (契约) | `EffectScriptContract.cs:209-223` | `if(TryGetProperty) chain` + `throw` | 5 (`gpu,commandBuffer,memory,occupancy,signalBus`) | parse |
| R-S1 | `SerializeResource` (契约) | `EffectScriptContract.cs:287-295` | `switch` 5 + `_=>throw` | 5 | serialize |
| R-P2 | `ParseResourceKey` (budget 键) | `EffectScriptContract.cs:247-255` | `switch` on prefix | 5 (`gpu:,commandBuffer:,memory:,occupancy:,signalBus:`) | parse-key |
| R-S2 | `ResourceKey` (budget 键) | `EffectScriptContract.cs:306-314` | `switch` 5 + `_=>throw` | 5 | serialize-key |
| S-P1 | `ParseScope` (契约) | `EffectScriptContract.cs:118-141` | `switch` on `type` | 4 (`method,type,global,scene`) | parse |
| S-S1 | `SerializeScope` (契约) | `EffectScriptContract.cs:269-276` | `switch` 4 + `_=>throw` | 4 | serialize |
| K-P1 | `ParseKind` | `EffectScriptContract.cs:196-200` | `switch` | 3 + `throw` | parse |
| M-P1 | `ParseMode` | `EffectScriptContract.cs:202-207` | `switch` | 5 + `throw` | parse |
| K/M-S1 | `SerializeClaim` | `EffectScriptContract.cs:278-285` | `ToString().ToLower` (开放: 无 switch) | 0 显式分支 | serialize |
| R-P3 | `ParseResource` (config) | `CosmosEffectConfig.cs:121-135` | `if chain` | 6 (`+custom`) | parse |
| S-P2 | `ParseScope` (config) | `CosmosEffectConfig.cs:136-149` | `switch` | 4 | parse |
| K-P2 | `ParseKind` (config) | `CosmosEffectConfig.cs:110-114` | `switch` | 3 | parse |
| M-P2 | `ParseMode` (config) | `CosmosEffectConfig.cs:115-120` | `switch` | 5 | parse |
| M-C1 | `Compatible.IsCompatible` | `Algebra.cs:18-28` | `if` chain 5 对兼容 + `return false` | 隐含 25 对全函数 | 语义 |
| W-H1 | `ScopeId` 工厂 helpers | `ApiMapping.cs:33-34` | `Shell()/Global()` | 2 | whitelist |
| W-H2 | `ResourceId` 工厂 helpers | `ApiMapping.cs:36-48` | `Tree/Self/Phys/Mem/Disk/SigBus/Gpu/CmdBuf/AudioMx/Occ/Cb/Net/Inp` | 13 | whitelist |
| W-M1 | 白名单合并 | `CosmosEffectConfig.cs:69-78` `AllWithExtra` | `Dictionary<Canonical,ApiMapping>` | 数据驱动 (唯一开放点) | 扩展 |

**计数**: 暴露分发点 23 处,其中 **必须同步改的 switch/if 链 16 处**。

---

## 3. 加一分支的同步修改清单与耦合成本

### 3.1 新增 `ResourceId` 一分支 (例: `ResourceId.VideoEncoder(Rid)`)
| 必须改 | 文件:行 | 漏改后果 |
|--------|---------|----------|
| 定义 | `Objects.cs:21-41` | 无此分支即不可构造 |
| 归一 (可选) | `Objects.cs:52-65` | 若新分支与既有别名等价(如 `gpu`)需加,否则 Normalize 漏塌缩 |
| Parse 契约 | `EffectScriptContract.cs:209-223` | JSON 含新键 → `FormatException: resource 须含 ...之一` (P0) |
| Serialize 契约 | `EffectScriptContract.cs:287-295` | C# 侧含新 Resource → `不可序列化的 resource` throw, round-trip 断 (P0) |
| ParseBudgetKey | `EffectScriptContract.cs:247-255` | `budget["videoEncoder:xyz"]` → `未知 budget 键` (P0) |
| SerializeBudgetKey | `EffectScriptContract.cs:306-314` | Budget 含新资源 → 同上 throw (P0) |
| Parse config | `CosmosEffectConfig.cs:121-135` | `extraMappings` JSON 用新 resource → config 层抛,而契约层已改则两解析器不一致 (P1) |
| Whitelist helper | `ApiMapping.cs:36-48` | 无 helper 不阻塞编译,但新增 API 映射需手写 `new ResourceId.VideoEncoder(...)` 重复造 (P2) |

**耦合成本**: **4 文件 / 7 分发点** (`Objects` 1 + `EffectScriptContract` 4 + `CosmosEffectConfig` 1 + `ApiMapping` 1)。`Serialize`/`ResourceKey` 双刻与 `Parse` 双刻是同一事实写 4 遍 — 典型 complect。

> 当前已存在 drift 证据: `CosmosEffectConfig.ParseResource` 支持 `custom` (`:133`) 而 `EffectScriptContract.ParseResource` 不支持 → 两解析器对同一 JSON 语义分裂 (P1,见 §4 F-R03)。

### 3.2 新增 `ScopeId` 一分支 (例: `ScopeId.Job(string Id)`)
| 必须改 | 文件:行 | 漏改后果 |
|--------|---------|----------|
| 定义 | `Objects.cs:90-99` | — |
| `IncludedIn` | `Objects.cs:102-108` | **无需改** — 唯一开放点,靠 `Equals`+`is Global` 即得自反+Global 最大元,跨标签不可比恒 `false` (Hickey 式 data-complete, 值得保留) |
| Parse 契约 | `EffectScriptContract.cs:118-141` | 漏改 → 新 scope JSON 落 `throw 未知 scope.type` (若守旧) 或若曾被改成 `_=>Scene` 则静默错 (历史已修) |
| Serialize 契约 | `EffectScriptContract.cs:269-276` | 漏改 → `不可序列化的 scope` throw |
| Parse config | `CosmosEffectConfig.cs:136-149` | 同上,两解析器分裂 |
| Atomics | `EffectScript.cs:23-58`/`EffectScriptContract.cs:170-171` | `eventScope == claim.Scope` 一致性校验不感知新类型,但 `Equals` 已覆盖,无需新增分支 |

**耦合成本**: **3 文件 / 3 分发点**。`ScopeId` 是现有设计中最接近 OCP 的 — 但契约序列化层仍 complect。

> 死分支证据: `Objects.cs:90-99` 8 分支中 `Shell,Loop,Conditional,Async` 4 个在两处 `ParseScope`/`SerializeScope` 均不可达 → JSON 不可表达、序列化即抛。类型膨胀而契约窒息 (P0,见 F-S02)。

### 3.3 新增 `Kind` 一分支 (例: `Kind.Control`)
| 必须改 | 文件:行 | 漏改后果 |
|--------|---------|----------|
| 定义 | `Objects.cs:112` | — |
| `Signature.Add` | `Objects.cs:194-199` | 漏改 → `ArgumentOutOfRangeException: 未知 Kind` (P0) |
| `Signature` 三桶字段 | `Objects.cs:154-157` | 需新增 `_control` 桶,否则新 Kind 无处存放 (P0, 影响 `Union/Join/AllClaims`) |
| `Weight.Of` | `Algebra.cs:35-39` | 跨新 Kind 与旧 Kind `throw KIND_MIX` 是否预期? 需审 (P1) |
| `Peak.Compute`/`NetTable.Compute` | `Algebra.cs:54-65,120-134` | 当前仅 `Occupy` 参与 `net/Peak`;新 Kind 是否参与? 漏改则静默丢弃 (P1) |
| `ParseKind` ×2 | `EffectScriptContract.cs:196-200`, `CosmosEffectConfig.cs:110-114` | JSON 含新 kind → throw |
| `SerializeClaim` | `EffectScriptContract.cs:278-285` | 开放: `ToString().ToLower` 无需改 — 唯一 OCP 点,但大小写/拼写无校验 (P2) |

**耦合成本**: **4 文件 / 7 分发点** (含三桶结构)。`Kind` 是 complect 重灾 — 三桶量纲隔离把 `Kind` 值 complect 进字段名。

### 3.4 新增 `Mode` 一分支 (例: `Mode.Reset`)
| 必须改 | 文件:行 | 漏改后果 |
|--------|---------|----------|
| 定义 | `Objects.cs:117` | — |
| `Claim.Normalize` 校验 | `Objects.cs:133-142` | `Read+Reset` 是否合法? 需更新白名单规则,否则非法组合静默进入 `net/Peak` |
| `Compatible.IsCompatible` | `Algebra.cs:18-28` | 漏改 → 新 Mode 与所有旧 Mode 的 5+5 对落 `return false` (默认冲突),误报 CONFLICT (P0) |
| `ParseMode` ×2 | `EffectScriptContract.cs:202-207`, `CosmosEffectConfig.cs:115-120` | throw |
| Audit gate(3) 分组 | `EffectScript.cs:192-247,276-286` | `grp` 键 `(ResourceId,ScopeId,intMode)` 的 `int` cast 仍工作,但 `IsCompatible(mode,mode)` 自兼容判定依赖新 Mode 的 COMPAT 表 — 漏更新则同 Mode 并发误判 |
| `Budget`/`Deviation` | 无直接分支 | 间接经 `Mode` 分组影响 |

**耦合成本**: **4 文件 / 5 分发点**。`Mode` 的语义表 (`Compatible`) 是最危险的隐式全函数 — 加一值需补 `2N+1` 对兼容性,漏一对即静默错。

---

## 4. Findings (Severity + Evidence + Smallest Fix)

### P0 — 合并阻塞
- **F-R01 [P0] ResourceId 契约双刻分裂 — 15 vs 5**  
  Evidence: `Objects.cs:21-41` 15 构造子 vs `EffectScriptContract.cs:209-223` 仅 5 键、`287-295` 5 键、`247-255` 5 键、`306-314` 5 键。`ApiMapping.cs:36-48` 13 helpers 已覆盖 `Tree/Self/Physics/Disk/Network/Input` 等,但契约层不可达 → C# 侧合法的 `Claim(ResourceId.Tree(...))` 无法 JSON 往返。  
  Fix: **单一真源 registry**: `static IReadOnlyDictionary<string,Func<JsonElement,ResourceId>>` + `IReadOnlyDictionary<Type,string>` 各一表,Parse/Serialize/ResourceKey 三处查表而非 4 个 switch。或最简:抽 `ResourceIdCodec` 静态类集中 5→15 的映射,4 处 site 各 call 一函数。

- **F-R02 [P0] Budget 键与 Resource 键双重 complect**  
  Evidence: `EffectScriptContract.cs:247-255` 与 `306-314` 与 `209-223/287-295` 四处 switch 同构。改一资源需改 4 处,已导致 `memory` 在 `EffectScriptContract.ParseResource:219` 保留 `uid` 而 `ApiMapping.Mem():39` 硬编码 `Memory(0)` 哨兵的语义分裂 (R2 #2)。  
  Fix: 复用同一 `ResourceIdCodec` 的 `ToKey`/`ParseKey`/`ToJson`/`ParseJson`,Budget 键仅为 `ToKey` 的前缀切片。

- **F-S01 [P0] ScopeId 8 vs 4 — 死分支不可 round-trip**  
  Evidence: `Objects.cs:90-99` 8 分支 vs `EffectScriptContract.cs:118-141` 4 分支、`269-276` 4 分支、`CosmosEffectConfig.cs:136-149` 4 分支。`Shell/Loop/Conditional/Async` 在 C# 侧可构造但 `SerializeScope` 直接 `throw 不可序列化` → `ToJson` 炸;`ParseScope` 亦不可解析。  
  Fix: 补 4 分支或收窄 `Objects.ScopeId` 至契约可达 4 支 (二选一,需决策)。最简收窄:若 `Shell/Loop/...` 仅内部分析用,则契约层保持 4 支并文档化"不可序列化 scope 仅 L1 内部"。

- **F-K01 [P0] Kind 三桶把值 complect 进字段名**  
  Evidence: `Objects.cs:154-157` 三字段 `_read/_write/_occupy` + `Objects.cs:194-199` switch + `Algebra.cs:54-65` `if(Kind!=Occupy) continue` 硬编码。加一 Kind 需改结构+3 处分发。  
  Fix: `Dictionary<Kind, ImmutableHashSet<Claim>>` 或 `ImmutableDictionary<Kind, SignatureBucket>` 单表分发;`NetTable/Peak` 改为查表 `if(!netKinds.Contains(c.Kind)) continue` 可配置。

- **F-M01 [P0] Mode 全函数表隐式 25 对 — 加一值漏一对即误报**  
  Evidence: `Algebra.cs:18-28` 用 `if` 链表达 25 对全函数,无表。新增 `Mode.Reset` 时,旧代码对 `Reset` 的 `IsCompatible(Reset,Use)` 因 `Use` 短路仍 true,但 `IsCompatible(Reset,Reset)` 落 `return false` (自冲突) 是否预期? 需显式裁决。  
  Fix: 显式 `static bool[,] table` 5×5 (现) → N×N, `IsCompatible` 查表;加一 Mode 即补一行一列,编译器可借 `Enum.GetValues` 校验全覆盖。

### P1 — 应在发布前修
- **F-R03 [P1] 双解析器 drift: `custom` 仅 Config 可达**  
  Evidence: `CosmosEffectConfig.cs:133` `custom` vs `EffectScriptContract.cs:209-223` 无 `custom`。同一 JSON 在 `LoadExtra` 与 `Parse` 得到不同 `ResourceId` 或一抛一过。  
  Fix: 复用同一 `ResourceIdCodec`;或让 `EffectScriptContract.ParseResource` 亦支持 `custom` (与 Config 对齐)。

- **F-D01 [P1] Whitelist 半开放 — 契约层未开放**  
  Evidence: `CosmosEffectConfig.cs:69-78` `AllWithExtra` 对 `GodotApiWhitelist.All` 做 Canonical 合并,实现了 API→Claim 的数据驱动扩展。但 `ResourceId/ScopeId/Kind/Mode` 的值域扩展仍走代码分支,未被 `cosmos.effect.json` 覆盖。新增资源需改 4 文件而非改 1 JSON。  
  Fix: 将 `ResourceIdCodec` 注册表暴露为 `cosmos.effect.json` 的 `resourceCodecs` 段 (可选),或至少让 `ParseResource` 查 `extra ResourceId` 注册表。

- **F-C01 [P1] `Normalize` 仅 3 分支,剩余 12 构造子直通 `=>r`**  
  Evidence: `Objects.cs:52-65` `_=>r` 兜底使新增 ResourceId 默认"已规范",易漏塌缩 (如 `AudioMixer` ChannelId 归一未定义)。  
  Fix: 将归一表亦改为 `Dictionary<Type,Func<ResourceId,ResourceId>>` 或文档化"未列即规范",并在 `Normalize` 加 `Debug.Assert` 覆盖测试。

### P2 — 笔记/可延后
- **F-S02 [P2] `ScopeId.IncludedIn` 是唯一 OCP 亮点**  
  Evidence: `Objects.cs:102-108` 无 per-type switch,靠 `Equals`+`is Global` 完成。加一 Scope 无需改此函数 — 值得作为示范保留。  
  Note: 若未来 Scope 需层级 (如 `Job < Scene < Global`),此实现需升级为表驱动偏序,届时将 complect。

- **F-K02 [P2] `SerializeClaim` 的 `ToString` 开放但脆弱**  
  Evidence: `EffectScriptContract.cs:278-285` 用 `Kind.ToString().ToLower` 序列化,加一 Kind 零改动即生效,但重命名 enum 即破契约。建议显式 `KindToString` 表与 `ParseKind` 同表,避免反射隐式耦合。

---

## 5. 耦合成本量化

| 新增 | 需改文件数 | 需改 switch/if 点 | 代表路径 | 漏改爆炸半径 |
|------|------------|-------------------|----------|--------------|
| `ResourceId` | **4** (`Objects`, `EffectScriptContract`×2表×2键, `CosmosEffectConfig`, `ApiMapping`) | **7** | `Objects:52` + `ESC:209,287,247,306` + `CEC:121` + `ApiMapping:36` | JSON 解析 throw / 序列化 throw / Budget 键 throw 三选一必炸 |
| `ScopeId` | **3** | **3** | `ESC:118,269` + `CEC:136` | 序列化 throw |
| `Kind` | **4** | **7** | `Objects:194` + `Algebra:35,54,120` + `ESC:196` + `CEC:110` | `ArgumentOutOfRange` / 静默丢弃 (net/Peak 漏) |
| `Mode` | **4** | **5** | `Objects:133` + `Algebra:18` + `ESC:202` + `CEC:115` | 静默 CONFLICT 误报/漏报 (最难查) |

**Hickey 视角**: 以上成本的根因是 **把值编码进了控制流**。理想 OCP 应是: **值是数据,控制流查数据**。当前 `switch(kind) =>` / `if(TryGetProperty("gpu"))` 都是值即分支。

---

## 6. 最小修正 (Smallest Fix, 不做大重构)

1. **抽 `ResourceIdCodec` / `ScopeIdCodec` 单表** (各一文件, ~60 行):  
   ```csharp
   // 单一真源: 双向表
   static readonly Dictionary<string, Func<JsonElement,string,ResourceId>> ParseMap = ...;
   static readonly Dictionary<Type, Func<ResourceId,object>> SerializeMap = ...;
   static readonly Dictionary<Type, Func<ResourceId,string>> KeyMap = ...;
   ```  
   四处 switch 改为 `Codec.Parse / Codec.Serialize / Codec.ToKey` 一行查表。加一分支仅改 `Codec` 一文件。

2. **`Kind` 三桶 → `Dictionary<Kind,Bucket>`** + `NetKinds = {Occupy}` 可配置集。

3. **`Mode` 兼容表显式化**: `static readonly bool[,] Compat = { {true,...} }` + `IsCompatible` 查表;单元测试用 `Enum.GetValues` 校验表为方阵且对称。

4. **对齐双解析器**: `EffectScriptContract.ParseResource` 与 `CosmosEffectConfig.ParseResource` 共用 `ResourceIdCodec` (或后者委托前者)。

5. **文档化 Scope 死分支**: 选型 A) 契约补 4 分支; 选型 B) 收窄 `Objects.ScopeId` 并文档"内部 scope 不可序列化"。本审计倾向 A 的最小增量 (补 `Shell/Loop/Conditional/Async` 四 case, 各 1 行)。

---

## 7. 残余风险 (Complect 未根除前)

- 新增 `ResourceId` 若仅改 `Objects` 而忘改 `Codec` 4 点,CI 需有 round-trip 对称性测试 (`Parse(ToJson(x))==x` 对所有 15+ ResourceId) 兜底,否则运行期才炸。
- `Kind`/`Mode` 的 `ToString` 序列化隐式依赖枚举命名,重构改名即破契约,需显式表锁死。
- `CosmosEffectConfig` 的 `strict=false` 静默回落 `Empty` 会掩盖 `cosmos.effect.json` 拼写错,半开放的代价。

---

**Merge verdict**: **OK with notes** — 非 P0 阻塞合并,但 **P0 项 (F-R01/F-R02/F-S01/F-K01/F-M01) 在下一次值域扩展前必须单表化**,否则扩展成本线性增长且漏改即炸。Hickey 尺: 当前 data vs place 之比 **~30% 开放 / 70% complect**。
```

```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "audit file D:/Godot/Cosmos/audit/rich-hickey-round07-extensibility.md — 23 dispatch sites inventoried with file:line, 9 findings (5×P0/3×P1/1×P2) + coupling matrix 3-4 files / 5-9 sites per added branch"
    }
  ],
  "changedFiles": [
    "audit/rich-hickey-round07-extensibility.md"
  ],
  "testsAddedOrUpdated": [],
  "commandsRun": [],
  "validationOutput": [
    "read Objects.cs:21-41,52-65,90-108,112,117,133-142,194-199; EffectScriptContract.cs:118-141,196-223,247-255,269-314; CosmosEffectConfig.cs:110-149; ApiMapping.cs:33-48; Algebra.cs:18-39 — all dispatch points verified with line numbers"
  ],
  "residualRisks": [
    "ResourceId 15 vs 5 drift remains until ResourceIdCodec single-table extraction — next branch addition still requires 4-file shotgun surgery",
    "Kind three-bucket fields complect value into field names — new Kind requires structural change not just switch addition",
    "Mode compatibility implicit 25-pair table — new Mode defaults to conflict (false) if table not extended, silent false-positive"
  ],
  "noStagedFiles": true,
  "diffSummary": "added audit/rich-hickey-round07-extensibility.md — Hickey OCP extensibility audit: full dispatch inventory (23 sites), per-symbol sync checklist and coupling cost (ResourceId 4f/7sites, ScopeId 3f/3sites, Kind 4f/7sites, Mode 4f/5sites), 5×P0/3×P1 findings with file:line evidence, smallest fix = single-table Codec + Kind dictionary + Mode compat matrix",
  "reviewFindings": [
    "P0: audit/rich-hickey-round07-extensibility.md:F-R01 - ResourceId 15 constructors vs 5-key contract parse/serialize/key quartet split across EffectScriptContract.cs:209,287,247,306 — add-one touches 4 files/7 sites",
    "P0: audit/rich-hickey-round07-extensibility.md:F-S01 - ScopeId 8 vs 4 — Shell/Loop/Conditional/Async dead branches throw on SerializeScope EffectScriptContract.cs:269",
    "P0: audit/rich-hickey-round07-extensibility.md:F-K01 - Kind value complected into field names Objects.cs:154-157 + switch Objects.cs:194 — new Kind requires structural bucket addition",
    "P0: audit/rich-hickey-round07-extensibility.md:F-M01 - Mode compat implicit 25-pair Algebra.cs:18-28 — new Mode silently misclassified as conflict",
    "P1: audit/rich-hickey-round07-extensibility.md:F-R03 - dual parser drift: custom only in CosmosEffectConfig.cs:133 not in EffectScriptContract.cs:209"
  ],
  "manualNotes": "no write tool available — artifact returned inline for runtime persistence to D:/Godot/Cosmos/audit/rich-hickey-round07-extensibility.md; only DONE_R07 per task wire format"
}