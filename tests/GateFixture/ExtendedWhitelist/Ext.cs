namespace ExtendedWhitelistFixture;

using Godot;

// QED-C1b 门禁存在性 fixture：白名单扩展经 AdditionalFiles 真接线。
// ExtLeaky：CustomSpawn（扩展 API）无配对 ⇒ EAA0901（error）⇒ 构建红——证明扩展映射真实参与 L3 分析；
// ExtPaired：CustomSpawn + CustomDespawn 配对 ⇒ 不报——证明扩展不引入误报。
// TreatWarningsAsErrors 同时兜底：有效配置不得产生任何 EAA0701（坏配置在此工程直接红）。
public static class Ext
{
    public static void ExtLeaky()
    {
        var g = new Node();
        g.CustomSpawn();
        // 无 CustomDespawn ⇒ EAA0901（error）⇒ 构建红
    }

    public static void ExtPaired()
    {
        var g = new Node();
        g.CustomSpawn();
        g.CustomDespawn(); // 配对 ⇒ 不报
    }
}
