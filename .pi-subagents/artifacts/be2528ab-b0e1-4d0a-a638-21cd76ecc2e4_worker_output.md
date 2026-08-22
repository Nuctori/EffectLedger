Audit #44 written to `audit/iter44.md` (verified on disk, 9353 bytes, header "独立审计 #44，hy3 单独进程").

**覆盖的 ED-*：**
- ED-001（L521-522，API 效应映射白名单/默认规则收敛声明）——核心，与 L516 实际默认规则矛盾
- 关联 ED-002..008 仅作为「已收敛」背景未展开（聚焦默认规则，未 widening）

**发现的高优先 open（3 个）：**
1. **PO-I44-a（高）**：默认规则 `{ read(unknown, use), write(unknown, use) }`（L516）kind 仅 read/write、mode 仅 use ⇒ 结构性永不产出 occupy/release ⇒ 释放类 unmapped API 系统漏报（交叉 Iter10/Iter27）。
2. **PO-I44-b（高）**：net(S)（L163-165）仅对 `kind=occupy` 求和，默认规则不产 occupy ⇒ 其对 net 恒贡献 0 ⇒ 创建-释放配对守恒在默认规则下既不可证真也不可证伪。
3. **PO-I44-d（高）**：默认规则致 DO-9 双误——unmapped 分配器 create 被吞→真实泄漏 fail-open 漏报；whitelisted AddChild(+size) 配 unmapped release(0)→误报泄漏 fail-false。

（另 1 个中优先：PO-I44-c，ED-001「默认最大效应」与 L516 实际「对 occupy 为最小」矛盾 ⇒ 收敛声明不实。）

补救草案 R1/R2/R3 均系**条件证明**（依赖 Iter21 Unknown 处理、Iter27 QueueFree mode 修正、Iter37 net(scope)），诚实标注未消解。