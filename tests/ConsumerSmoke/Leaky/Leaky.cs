// 打包消费烟测——泄漏样例（构建必须失败）：
// Godot 白名单 acquire（AddChild）无同方法 release 配对 ⇒ EAA0901 warning；
// TreatWarningsAsErrors 把 warning 升 error ⇒ dotnet build 非零退出。
// 这证明分析器经真实 NuGet 包链（PackageReference → analyzers/dotnet/cs）真实加载并参与编译期门禁
//（裸 ProjectReference 或包未落 analyzers/ 路径时静默零诊断 ⇒ 此工程会假绿构建成功，烟测立即抓住）。
namespace Godot
{
    public class Node
    {
        public Node AddChild(Node n) => this;
        public void QueueFree() { }
    }
}

public static class LeakyScene
{
    public static void Leak()
    {
        var n = new Godot.Node();
        n.AddChild(new Godot.Node()); // acquire 无配对 release ⇒ EAA0901（error 化 ⇒ 构建失败）
    }
}
