# Rich Hickey 对抗性审计 · Round 04 — API as Data（EffectScript 数据契约）

透镜：Data Orientation。核心问题：**EffectScript 作为纯数据值，能否被 JSON 无损往返？契约边界是 fail-fast 还是 fail-soft？魔法字符串是否被类型收口？**

审计范围（7+1 文件，未读 audit/）：
`EffectScript.cs`、`EffectScriptContract.cs`、`Objects.cs`、`Algebra.cs`、`Numeric.cs`、`EffectAttributes.cs`、`ApiMapping.cs`、`EFFECT_SCRIPT.md` §4。
行号以当前磁盘版本为准。

---

## 一、结论速览

| # | 符号 / 位置 | 问题 | 严重度 |
|---|---|---|---|
| B1 | `EffectScript.Audit` 扫换线 gate(2)，EffectScript.cs:187,206 | `loop=0` ⇒ **DivideByZeroException 崩溃**；而 §4 契约示例本身就写了 `"loop": 0` | **Blocker** |
| H1 | `SerializeScope` 兜底臂，EffectScriptContract.cs:199 | `Shell/Loop/Conditional/Async` 四种 scope 静默序列化为 `global`，往返即数据损坏 | High |
| H2 | `SerializeResource` / `ResourceKey` 兜底臂，EffectScriptContract.cs:218,236 | 10 种未处理 ResourceId 构造子静默改写为 `memory:0` —— 典型 fail-soft 静默兜底 | High |
| H3 | `ParseEvent` loop 缺省，EffectScriptContract.cs:58 | 字段名打错（如 `loops`）或缺失 ⇒ 静默按 ω=1 审计，可掩盖峰值违例 | High |
| H4 | `ParseResource` 多键对象，EffectScriptContract.cs:148–158 | 同时含多个资源键时取第一个命中、其余静默丢弃；多余未知键不报错 | High |
| M1 | `ParseClaim`/`ParseScope`/`ParseInterval` | 异常类型与文档承诺的 FormatException 不一致（InvalidOperationException / ArgumentException 泄漏） | Medium |
| M2 | Parse 与 Serialize 双侧字面量 | kind/mode/scope.type/resource 前缀魔法字符串两侧重复、无单一真源 | Medium |
| M3 | `Violation.Kind`:string，EffectScript.cs:349 + :223,:231,:287 | 违例类型是字符串字面量；且三种违例硬编码 `ScopeId.Global` 丢弃真实 scope | Medium |
| M4 | `ParseBudget`，EffectScriptContract.cs:166 | budget 值不接受 ⊤、非整数抛 InvalidOperationException —— 与 lifetime/loop 哨兵处理不对称 | Medium |
| M5 | `ParseResourceKey` memory 空后缀，EffectScriptContract.cs:176 | `"memory:"` 静默映射 Memory(0)，与白名单哨兵 uid=0 撞键 | Medium |
| L1–L7 | 见第四节 | ⊤ 非 ASCII 哨兵、JsonDocument 未 dispose、重复 JSON 键容忍、集合归一化往返、死代码、字典序不确定、Unknown 放行 | Low |

---

## 二、Blocker

### B1 — `loop=0` 使 Audit 抛 DivideByZeroException（契约示例即可触发）

- 位置：`src/Cosmos.EffectAlgebra/EffectScript.cs:187`（enter 路径）与 `:206`（exit 路径）：
  ```csharp
  var mul = (!hi.IsTop && !w.IsTop && hi.Value <= ulong.MaxValue / w.Value) ? ...
  ```
- `LoopCount.Of(0)` 合法（DerivedMetrics.cs:19 无 ≥1 约束），`ParseLoop` 接受任意 uint64 含 0；**EFFECT_SCRIPT.md §4 示例第二个 event 就是 `"loop": 0`**——该示例只因 mode=release 被 `c.Mode != Mode.Release` 守卫侥幸绕开。任何 `mode∈{create,move}` 且 size.Hi 有限的 claim 配 `"loop": 0` ⇒ `ulong.MaxValue / 0` 未捕获崩溃。
- Data Orientation 判定：这是「数据驱动一切」路径上的隐藏前置条件（ω≥1）未被类型承载——`LoopCount` 类型没有排除 0，却让下游除法假设它非零。要么类型层拒绝 0（构造子 fail-fast），要么运算层把 0 当保守 ⊤。当前两者皆无。

## 三、High

### H1 — 四种 scope 构造子往返即损坏（SerializeScope 兜底）

- `EffectScriptContract.cs:194–199`：switch 只覆盖 `Scene/Method/Type`，`_ => {"type":"global"}` 把 `Shell/Loop/Conditional/Async`（Objects.cs 定义的全部 8 个构造子中的另外 4 个）**静默改写为 Global**。Global 是偏序最大元（Objects.cs `IncludedIn`），语义完全不同；`Parse` 也无从还原。
- 触发面：程序化构造的 `EffectEvent`（如携带 `ScopeId.Shell()`）→ `ToJson` → `Parse` ⇒ 数据被无声篡改。JSON 层只该承载它能表达的形状，表达不了的必须 throw，而不是兜底成最大元。

### H2 — 10 种资源构造子静默兜底为 memory:0

- `EffectScriptContract.cs:218`（`_ => new Dictionary{["memory"]=0}`）与 `:236`（`_ => "memory:0"`）。
- `ResourceId` 共 16 个构造子（Objects.cs）；契约只支持 5 个。其余（Tree/Self/Physics/Disk/Signal/AudioMixer/Callback/Network/Input/Custom）经 `ToJson` 全部**无声变成 Memory(0)**——不同资源被折叠为同一键，net/peak 归因全错且无任何信号。这正是任务点名的「resource 静默兜底」：正确形态是 `_ => throw new FormatException(...)`（fail-closed），兜底比报错危险得多。

### H3 — loop 缺省/近形字段名静默按 ω=1

- `EffectScriptContract.cs:58`：`TryGetProperty("loop")` 失败 ⇒ `LoopCount.Of(1)`。
- AI 写 `"loops": 8` 或漏写 ⇒ 按 ω=1 审计，峰值低估 8 倍，PeakExceeded 漏报。缺省 ω=1 是 §2.1 的合法语义，但「合法缺省」与「疑似拼写错误」在解析层不可区分时，至少应拒绝同对象内未知键（见 L3/H4 同根问题）。size 缺省⇒[1,1] 有 §3.1.5(a) 明文背书，风险较低但同属此类。

### H4 — 多键 resource 对象静默丢弃

- `EffectScriptContract.cs:148–158`：先做「至少含一键」的存在性检查，随后 if 链按 gpu→commandBuffer→memory→occupancy→signalBus 取首个命中；`{"gpu":"a","memory":1}` ⇒ Gpu(a)，memory 键无声消失。数据契约对超集形状应拒绝而非截断。

## 四、Medium / Low 逐符号表

### Medium

| 符号 | 位置 | 发现 |
|---|---|---|
| `ParseClaim` kind/mode 提取 | Contract.cs:124,126 | `Require(c,"kind").GetString() ?? throw`：`{"kind":123}` 时 GetString() 抛 **InvalidOperationException**，非文档承诺的 FormatException（文件头注释 :7 与 Parse doc :20 均承诺 FormatException）|
| `ParseScope` name 提取 | Contract.cs:90 | `{"scene":42}` ⇒ GetString() 抛 InvalidOperationException；`[⊤,5]`、lo>hi 等 interval 不变量违规由 Numeric.cs:66–74 抛 **ArgumentException**。fail-fast 成立，但异常面三分裂，「非法形状⇒FormatException」契约失真 |
| kind/mode/scope.type/resource 前缀魔法串 | Contract.cs:96–102 vs 195–199；164–167 vs 204；170–175 vs 206；172–181 vs 229–235 | 双侧 switch 字面量各自维护，无共享常量/单源；`Kind.ToString().ToLowerInvariant()` 还把线格式耦合到枚举标识符拼写。新增枚举成员 ⇒ Parse 抛（好）但 Serialize 先静默走 H1/H2 兜底（坏）——不对称漂移 |
| `Violation.Kind` + 硬编码 Global | EffectScript.cs:349（定义）、:223,:231,:287（生产点）、:245 附近 CompatibleConflict | 违例类型应为 enum/判别联合；NegativeDip/PearkExceeded/Leak 一律填 `ScopeId.Global()`，事件真实 scope 信息在数据产出端就被丢弃，AI 回修拿不到归因 scope |
| `ParseBudget` | Contract.cs:162–168 | 值仅接受有限整数；budget 无法显式表达 ⊤（无上限），与 lifetime/loop 接受 `"⊤"` 不对称；`prop.Value.GetUInt64()` 对字符串/小数抛 InvalidOperationException |
| `ParseResourceKey` memory 空后缀 | Contract.cs:176 | `"memory:"` ⇒ Memory(0)，与 ApiMapping.cs `Mem()` 白名单哨兵 uid=0 撞键——两个不同来源的「空」折叠为同一资源 |

### Low / Note

| 符号 | 位置 | 发现 |
|---|---|---|
| `"⊤"` 哨兵 | Contract.cs:79,108 | 非 ASCII 精确匹配，无别名（top/inf/null 均拒）。fail-fast 可接受，但契约文档应写明唯一拼法 |
| `Parse` | Contract.cs:23 | `JsonDocument.Parse` 结果未 dispose/using —— pooled 缓冲延迟回收，纯资源卫生问题 |
| 重复 JSON 键 | 全解析路径 | System.Text.Json 容忍重复属性名，TryGetProperty 取末值；歧义输入被静默接受 |
| 往返 = 模集合归一化 | Contract.cs:141–146 + Objects.cs Signature(ImmutableHashSet) | claim 顺序不保留、重复相同 claim 折叠。语义上有据（∪ 幂等交换），但「无损往返」严格意义上是 modulo set-normalization，应在 §4 文档声明 |
| 死代码 | EffectScript.cs:272 与 274 | `if (e.Lifetime.Lo.IsTop) continue;` 连续重复两行 |
| Budget.Caps 序 | Contract.cs:228–234 | Dictionary 序不定 ⇒ ToJson 字节序跨运行不稳定，与 §5 确定性目标在字节层面相悖（语义不受影响）|
| Mode.Unknown 放行 | Contract.cs:173 + Algebra.cs `Resolve` | 契约接受 `"unknown"`，随后被当 Use 弱化兼容——边界层放行一个语义上等于「没写」的值，宜在 Parse 拒绝或显式标注 |

## 五、正确的部分（值得肯定）

- `ResourceId`/`ScopeId` 判别联合用 record 单点建模（Objects.cs），`Normalize` 作为**显式命名的非结构相等函数**独立于 Equals——Hickey 式「identity vs equality 分离」的正确落地；Normalize 对 SignalBus 不二次剥前缀保幂等（Objects.cs 注释明确）。
- `Interval` 构造子强制 lo≤hi、lo 有限（Numeric.cs:60–75）：不变量进类型，不在消费端 if。
- `NatStar` 加/乘溢出 ⇒ 保守 ⊤（Numeric.cs:36–52），Audit 峰值路径同样溢出⇒⊤（EffectScript.cs:187 的意图正确，只是漏了除零）。
- `Claim` 五参位置记录 + `Size` 用可空区分「缺省」与「Exact(0)」（Objects.cs，Normalize 注释）——避免了 null/default 膨胀的经典坑。
- `Parse` 对未知 kind/mode/scope.type/budget 键/resource 缺值全部抛（ReqStr fail-fast），resource 值不再静默兜底（auditR2/R4 修正在场）。
- Global round-trip 已修（Contract.cs:88–93,101：`{"type":"global"}` 无 scene ⇄ ScopeId.Global），专项验证过对称性。

## 六、残余风险

1. **B1 未修前，契约文档 §4 示例本身是不可安全执行的形状族**（loop:0 × 非release claim 即崩）。
2. H1/H2 兜底意味着「ToJson 输出永远可被 Parse 读回」，但不保证「读回的是同一份数据」——当前测试若只测 Parse∘ToJson∘Parse 幂等而不比对语义等价，会漏掉这两处。
3. 异常面（M1）修复属破坏性变更（调用方若已捕获 ArgumentException/InvalidOperationException 会失配），需一次性收口并更新 §4 文档承诺。
