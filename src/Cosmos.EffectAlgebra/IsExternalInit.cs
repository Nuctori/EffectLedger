// netstandard2.0 兼容垫片：init 访问器所需的 IsExternalInit 类型（net10.0 自带，netstandard2.0 缺）。
// 仅当分析器 TFM (netstandard2.0) 编译时参与；net10.0 走系统自带，不冲突（同名同命名空间）。
namespace System.Runtime.CompilerServices
{
    using System.ComponentModel;
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit { }
}
