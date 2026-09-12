# 安全策略

## 报告漏洞

如发现安全问题，请**不要开公开 Issue**，改用 GitHub 的
[Private vulnerability reporting](https://github.com/Nuctori/EffectLedger/security/advisories/new)
（仓库 Security 标签页 → Report a vulnerability）。

请包含：影响的版本/提交、复现步骤、最小可复现样例（若适用）、你评估的影响面。

## 本项目特有的「安全」边界（请先读）

本库是**静态分析工具**，不是安全边界机制。以下性质是**设计如此**，不属于漏洞：

1. **静态近似盲区**：EAA* 诊断是**近似**，会有漏报（跨方法/跨对象配对、构造期泄漏、
   未覆盖的控制流形态）。权威判定是运行期 Σnet 闭合。README「诚实边界」20 条逐条列明。
2. **internal 面是文档性边界而非安全边界**：`InternalsVisibleTo` 按程序集**简单名**匹配，
   未强命名。同名程序集可访问 internal 类型——这是已知且接受的（见
   `audit/p5-adversarial-meeting.md` 的 H2 决策备忘录：强签名与开源分发冲突）。
   **不要依赖 internal 做隔离。**
3. **分析器宿主限制**：Visual Studio / .NET Framework MSBuild 的 Roslyn 宿主不加载本分析器
   （静默零诊断）。请用 `dotnet build`。这不是漏洞而是宿主能力限制。
4. **Runtime 非线程安全**：帧驱动单线程模型，零锁。全部调用须在宿主主线程
   （README 诚实边界 #10）。

## 支持版本

本项目处于 pre-release，尚未在 nuget.org 发布。当前仅 `master` 分支受支持；
安全问题会在修复后进入下一版本并在 `CHANGELOG.md` 记录。
