iter49 written. Reporting as required.

**两 Peak 统一草案要点（V5）：**
1. 以 `peak_size(S,scope)=max_{t⊆*scope} Σ_{c: c.scope⊆*t, mode≠release} c.size` 为**唯一规范**峰值口径（Σsize 资源占用），scope 过滤改用 Iter34 的 ⊆* 偏序、补 t 窗口枚举。
2. §3.2.5 的 `Peak`（计数并发条数）退化为 `peak_size` 在「∀c.size≡1 且不分桶」下的**计数特例** `Peak_count`，量纲明确为条数。
3. **迭代维**：若 `S` 已按 Iter35 预展开 `S×ω`（copy_j 打标），则并发峰值须"先按迭代 i 分组求和、再跨 t 取 max"——保留 Peak 的 max-over-iteration，而非 peak 的全局求和（二者在迭代维聚合相反，不能简单替换）。
4. **分桶**：统一后的 peak 须按 Iter36 的 `(R,W,O)` 分桶分别求，消除 DO-7 违反。

**发现的高优先 open（5 个，全高）：**
- I49-01：两峰值量纲/概念不等价（计数 vs Σsize），文档未说明关系。
- I49-02：两者都依赖未定义的 `ScopeId⊆`（`c.scope⊆scope` / `t∈scope`），同悬空（交叉 Iter15/34）。
- I49-03：`Peak` 的 max-over-iteration 与展开后 `peak` 的 sum-over-scope 在迭代维聚合相反（交叉 Iter18/35）。
- I49-04：两峰值都未按 kind 分桶 ⇒ 跨 read/write/occupy 混算，违 DO-7（交叉 Iter36/14）。
- I49-05（弱）：两者都叫"Peak/peak"仅大小写差，命名混淆，统一时应重命名。

所有 5 项均为 open（草案依赖 Iter34/35/36 全部未立），诚实标注未消解。