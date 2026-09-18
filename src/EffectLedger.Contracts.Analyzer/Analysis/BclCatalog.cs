// BclCatalog.cs — P4.3 BCL 精确目录（最小集合，按符号判定，不做字符串前缀扫描）。
// 原则（需求 R-BOUNDARY-03）：不全域信任 System.*，不读 [Pure] 当证明，不做名称关键词匹配。
// 本目录按"精确类型名 + 成员名"匹配，命中才给结论；未命中 ≡ 无目录依据（由调用方决定 → 未知还是放行）。
//
// 刻意保守：
//   - 目录缺失 => 返回 Unknown（保守），不是"纯"。
//   - 同一成员的"看似安全重载"和"危险重载"分开登记。

using System.Collections.Generic;

namespace EffectLedger.Contracts.Analyzer.Analysis;

/// <summary>目录结论：一个精确符号的行为分类。</summary>
public readonly struct CatalogEntry
{
    public HiddenInputKind HiddenInputs { get; }
    public ExternalEffectKind ExternalEffects { get; }

    /// <summary>true = 该成员是已知确定/无外部效果的（可安全用于确定性计算）。</summary>
    public bool IsKnownSafe { get; }

    public CatalogEntry(HiddenInputKind hidden, ExternalEffectKind effect, bool knownSafe)
    {
        HiddenInputs = hidden;
        ExternalEffects = effect;
        IsKnownSafe = knownSafe;
    }

    public static readonly CatalogEntry Safe = new(HiddenInputKind.None, ExternalEffectKind.None, true);
    public static CatalogEntry WithHidden(HiddenInputKind k) => new(k, ExternalEffectKind.None, false);
    public static CatalogEntry WithEffect(ExternalEffectKind k) => new(HiddenInputKind.None, k, false);
}

/// <summary>
/// 精确 BCL 目录。键 = "命名空间.类型名::成员名"（不含重载签名，重载按成员级统一处置；
/// 需要区分重载时必须细化为独立条目，见 P4.3 测试）。
/// </summary>
public static class BclCatalog
{
    /// <summary>目录版本（P5.6）：条目增补递增 1.x；判定语义变更递增 2.0。
    /// 报告记录它以区分"同一引擎不同目录"的结论。</summary>
    public const string Version = "1.0.0";

    private static readonly Dictionary<string, CatalogEntry> Entries = new()
    {
        // ── 隐藏输入：时钟（含值成员，避免只禁类型）──
        ["System.DateTime::get_Now"] = CatalogEntry.WithHidden(HiddenInputKind.Time),
        ["System.DateTime::get_UtcNow"] = CatalogEntry.WithHidden(HiddenInputKind.Time),
        ["System.DateTime::get_Today"] = CatalogEntry.WithHidden(HiddenInputKind.Time),
        ["System.DateTimeOffset::get_Now"] = CatalogEntry.WithHidden(HiddenInputKind.Time),
        ["System.DateTimeOffset::get_UtcNow"] = CatalogEntry.WithHidden(HiddenInputKind.Time),
        ["System.Diagnostics.Stopwatch::StartNew"] = CatalogEntry.WithHidden(HiddenInputKind.Time),
        ["System.Diagnostics.Stopwatch::GetTimestamp"] = CatalogEntry.WithHidden(HiddenInputKind.Time),
        ["System.Environment::get_TickCount"] = CatalogEntry.WithHidden(HiddenInputKind.Time),
        ["System.Environment::get_TickCount64"] = CatalogEntry.WithHidden(HiddenInputKind.Time),

        // ── 隐藏输入：随机 ──
        ["System.Random::Next"] = CatalogEntry.WithHidden(HiddenInputKind.Random),
        ["System.Random::NextBytes"] = CatalogEntry.WithHidden(HiddenInputKind.Random),
        ["System.Random::NextDouble"] = CatalogEntry.WithHidden(HiddenInputKind.Random),
        ["System.Random::NextSingle"] = CatalogEntry.WithHidden(HiddenInputKind.Random),
        ["System.Random::NextInt64"] = CatalogEntry.WithHidden(HiddenInputKind.Random),
        ["System.Random::get_Shared"] = CatalogEntry.WithHidden(HiddenInputKind.Random),
        ["System.Guid::NewGuid"] = CatalogEntry.WithHidden(HiddenInputKind.Random),

        // ── 隐藏输入：环境 / 进程 ──
        ["System.Environment::GetEnvironmentVariable"] = CatalogEntry.WithHidden(HiddenInputKind.Ambient),
        ["System.Environment::get_CurrentDirectory"] = CatalogEntry.WithHidden(HiddenInputKind.Ambient),
        ["System.Environment::get_MachineName"] = CatalogEntry.WithHidden(HiddenInputKind.Ambient),
        ["System.Environment::get_UserName"] = CatalogEntry.WithHidden(HiddenInputKind.Ambient),
        ["System.Environment::get_ProcessorCount"] = CatalogEntry.WithHidden(HiddenInputKind.Ambient),
        ["System.Environment::get_StackTrace"] = CatalogEntry.WithHidden(HiddenInputKind.Ambient),

        // ── 隐藏输入：文化 / 区域（跨进程不确定）──
        ["System.Globalization.CultureInfo::get_CurrentCulture"] = CatalogEntry.WithHidden(HiddenInputKind.Culture),
        ["System.Globalization.CultureInfo::get_CurrentUICulture"] = CatalogEntry.WithHidden(HiddenInputKind.Culture),
        ["System.Globalization.CultureInfo::get_InvariantCulture"] = CatalogEntry.Safe,
        ["System.Globalization.CultureInfo::get_DefaultThreadCurrentCulture"] = CatalogEntry.WithHidden(HiddenInputKind.Culture),

        // ── 外部效果：控制台 / 日志 ──
        ["System.Console::Write"] = CatalogEntry.WithEffect(ExternalEffectKind.Console),
        ["System.Console::WriteLine"] = CatalogEntry.WithEffect(ExternalEffectKind.Console),
        ["System.Console::get_Out"] = CatalogEntry.WithEffect(ExternalEffectKind.Console),
        ["System.Console::get_Error"] = CatalogEntry.WithEffect(ExternalEffectKind.Console),
        ["System.Console::ReadLine"] = CatalogEntry.WithEffect(ExternalEffectKind.Console),

        // ── 外部效果：文件 ──
        ["System.IO.File::ReadAllText"] = CatalogEntry.WithEffect(ExternalEffectKind.File),
        ["System.IO.File::ReadAllBytes"] = CatalogEntry.WithEffect(ExternalEffectKind.File),
        ["System.IO.File::WriteAllText"] = CatalogEntry.WithEffect(ExternalEffectKind.File),
        ["System.IO.File::WriteAllBytes"] = CatalogEntry.WithEffect(ExternalEffectKind.File),
        ["System.IO.File::get_Exists"] = CatalogEntry.WithEffect(ExternalEffectKind.File),
        ["System.IO.File::Delete"] = CatalogEntry.WithEffect(ExternalEffectKind.File),
        ["System.IO.Directory::get_Exists"] = CatalogEntry.WithEffect(ExternalEffectKind.File),
        ["System.IO.Directory::CreateDirectory"] = CatalogEntry.WithEffect(ExternalEffectKind.File),
        ["System.IO.Path::GetTempPath"] = CatalogEntry.WithEffect(ExternalEffectKind.File),

        // ── 外部效果：网络 ──
        ["System.Net.Http.HttpClient::GetAsync"] = CatalogEntry.WithEffect(ExternalEffectKind.Network),
        ["System.Net.Http.HttpClient::PostAsync"] = CatalogEntry.WithEffect(ExternalEffectKind.Network),
        ["System.Net.Http.HttpClient::SendAsync"] = CatalogEntry.WithEffect(ExternalEffectKind.Network),
        ["System.Net.WebClient::DownloadString"] = CatalogEntry.WithEffect(ExternalEffectKind.Network),
        ["System.Net.Sockets.Socket::Connect"] = CatalogEntry.WithEffect(ExternalEffectKind.Network),

        // ── 外部效果：进程环境修改 ──
        ["System.Environment::SetEnvironmentVariable"] = CatalogEntry.WithEffect(ExternalEffectKind.Process),
        ["System.Environment::Exit"] = CatalogEntry.WithEffect(ExternalEffectKind.Process),

        // ── 已知安全的只读属性（无隐藏输入、无外部效果）──
        // 缺少这些会让最常见的 `_list.Count` 落到 Unknown（审计第三轮遗留的假红）。
        ["System.Collections.Generic.List`1::get_Count"] = CatalogEntry.Safe,
        ["System.Collections.Generic.Dictionary`2::get_Count"] = CatalogEntry.Safe,
        ["System.Collections.Generic.HashSet`1::get_Count"] = CatalogEntry.Safe,
        ["System.Collections.Generic.Queue`1::get_Count"] = CatalogEntry.Safe,
        ["System.Collections.Generic.Stack`1::get_Count"] = CatalogEntry.Safe,
        ["System.Array::get_Length"] = CatalogEntry.Safe,
        ["System.Array::get_LongLength"] = CatalogEntry.Safe,
        ["System.String::get_Length"] = CatalogEntry.Safe,
        ["System.Collections.Generic.List`1::get_Capacity"] = CatalogEntry.Safe,
        ["System.Text.StringBuilder::get_Length"] = CatalogEntry.Safe,

        // ── 常见只读/复制操作（无隐藏输入、无外部效果）──
        ["System.Collections.Generic.List`1::ToArray"] = CatalogEntry.Safe,
        ["System.Collections.Generic.List`1::Contains"] = CatalogEntry.Safe,
        ["System.Collections.Generic.List`1::IndexOf"] = CatalogEntry.Safe,
        ["System.Collections.Generic.List`1::get_Item"] = CatalogEntry.Safe,
        ["System.Collections.Generic.Dictionary`2::ContainsKey"] = CatalogEntry.Safe,
        ["System.Collections.Generic.Dictionary`2::TryGetValue"] = CatalogEntry.Safe,
        ["System.Array::GetLength"] = CatalogEntry.Safe,
        ["System.Object::ToString"] = CatalogEntry.Safe,
        ["System.Object::Equals"] = CatalogEntry.Safe,
        ["System.Object::get_Type"] = CatalogEntry.Safe,
        ["System.String::Concat"] = CatalogEntry.Safe,
        ["System.String::Substring"] = CatalogEntry.Safe,
        ["System.String::get_Chars"] = CatalogEntry.Safe,
        ["System.Math::Abs"] = CatalogEntry.Safe,
        ["System.Math::Min"] = CatalogEntry.Safe,
        ["System.Math::Max"] = CatalogEntry.Safe,
        ["System.Math::Floor"] = CatalogEntry.Safe,
        ["System.Math::Ceiling"] = CatalogEntry.Safe,
        ["System.Math::Round"] = CatalogEntry.Safe,
        ["System.Math::Sqrt"] = CatalogEntry.Safe,
        ["System.Math::Pow"] = CatalogEntry.Safe,
        // 基元类型的 ToString/Parse：无文化参数的 ToString 对整数/布尔是稳定的；
        // 但 decimal/double 的默认 ToString 受文化影响 ⇒ 显式登记为文化敏感。
        ["System.Int32::ToString"] = CatalogEntry.Safe,
        ["System.Int64::ToString"] = CatalogEntry.Safe,
        ["System.Int16::ToString"] = CatalogEntry.Safe,
        ["System.Byte::ToString"] = CatalogEntry.Safe,
        ["System.Boolean::ToString"] = CatalogEntry.Safe,
        ["System.Char::ToString"] = CatalogEntry.Safe,
        ["System.Decimal::ToString"] = CatalogEntry.WithHidden(HiddenInputKind.Culture),
        ["System.Double::ToString"] = CatalogEntry.WithHidden(HiddenInputKind.Culture),
        ["System.Single::ToString"] = CatalogEntry.WithHidden(HiddenInputKind.Culture),
        ["System.DateTime::ToString"] = CatalogEntry.WithHidden(HiddenInputKind.Culture),
        ["System.DateTimeOffset::ToString"] = CatalogEntry.WithHidden(HiddenInputKind.Culture),

        // 明确文化无关的字符串操作。
        ["System.String::ToUpperInvariant"] = CatalogEntry.Safe,
        ["System.String::ToLowerInvariant"] = CatalogEntry.Safe,
        ["System.String::Equals"] = CatalogEntry.Safe,
        ["System.String::CompareOrdinal"] = CatalogEntry.Safe,

        // ── 基元比较/哈希（审计第十轮 FP12：用户 Equals/GetHashCode/CompareTo 内部
        //    调用基元版本曾落 Unknown ⇒ 合法值类型比较无法通过 strict）──
        ["System.Int32::GetHashCode"] = CatalogEntry.Safe,
        ["System.Int64::GetHashCode"] = CatalogEntry.Safe,
        ["System.Int32::CompareTo"] = CatalogEntry.Safe,
        ["System.Int64::CompareTo"] = CatalogEntry.Safe,
        ["System.String::CompareTo"] = CatalogEntry.Safe,
        ["System.Int32::Equals"] = CatalogEntry.Safe,
        ["System.Int64::Equals"] = CatalogEntry.Safe,

        // ── LINQ 无比较器子集（审计第十轮 FP13：Where/Select/Sum 等纯查询曾全部 Unknown）。
        //    OrderBy/Min/Max 走默认比较器（string 文化敏感），刻意**不**登记 ⇒ Unknown。──
        ["System.Linq.Enumerable::Where"] = CatalogEntry.Safe,
        ["System.Linq.Enumerable::Select"] = CatalogEntry.Safe,
        ["System.Linq.Enumerable::SelectMany"] = CatalogEntry.Safe,
        ["System.Linq.Enumerable::ToList"] = CatalogEntry.Safe,
        ["System.Linq.Enumerable::ToArray"] = CatalogEntry.Safe,
        ["System.Linq.Enumerable::Sum"] = CatalogEntry.Safe,
        ["System.Linq.Enumerable::Count"] = CatalogEntry.Safe,
        ["System.Linq.Enumerable::Any"] = CatalogEntry.Safe,
        ["System.Linq.Enumerable::All"] = CatalogEntry.Safe,
        ["System.Linq.Enumerable::First"] = CatalogEntry.Safe,
        ["System.Linq.Enumerable::FirstOrDefault"] = CatalogEntry.Safe,
        ["System.Linq.Enumerable::Last"] = CatalogEntry.Safe,
        ["System.Linq.Enumerable::LastOrDefault"] = CatalogEntry.Safe,
        ["System.Linq.Enumerable::Single"] = CatalogEntry.Safe,
        ["System.Linq.Enumerable::SingleOrDefault"] = CatalogEntry.Safe,
        ["System.Linq.Enumerable::Skip"] = CatalogEntry.Safe,
        ["System.Linq.Enumerable::Take"] = CatalogEntry.Safe,
        ["System.Linq.Enumerable::Distinct"] = CatalogEntry.Safe,
        ["System.Linq.Enumerable::Contains"] = CatalogEntry.Safe,
        ["System.Linq.Enumerable::Aggregate"] = CatalogEntry.Safe,
        ["System.Linq.Enumerable::Reverse"] = CatalogEntry.Safe,

        // ── 其它高频纯成员（FP13）──
        ["System.String::Join"] = CatalogEntry.Safe,
        ["System.Guid::ToString"] = CatalogEntry.Safe,
        ["System.Text.StringBuilder::ToString"] = CatalogEntry.Safe,
        ["System.Collections.Generic.Dictionary`2::get_Values"] = CatalogEntry.Safe,
        ["System.Collections.Generic.Dictionary`2::get_Keys"] = CatalogEntry.Safe,
        ["System.Collections.Generic.Dictionary`2::get_Item"] = CatalogEntry.Safe,
        ["System.Collections.Generic.HashSet`1::Contains"] = CatalogEntry.Safe,
        ["System.Collections.Generic.Queue`1::Peek"] = CatalogEntry.Safe,
        ["System.Collections.Generic.Stack`1::Peek"] = CatalogEntry.Safe,

        // ── 文档推荐的集合迁移目标（两份用户视角审计的头条）──
        // docs 明确建议"改用 IReadOnlyList<T> / ImmutableArray<T> 传递集合输入"，
        // 但此前这两族的成员一个都没登记 ⇒ 照文档改会从"入口被拒"掉进"成员 Unknown"。
        // 只读视图接口的成员读取无副作用（容器不可变由角色检查负责）。
        ["System.Collections.Generic.IReadOnlyList`1::get_Count"] = CatalogEntry.Safe,
        ["System.Collections.Generic.IReadOnlyList`1::get_Item"] = CatalogEntry.Safe,
        ["System.Collections.Generic.IReadOnlyList`1::GetEnumerator"] = CatalogEntry.Safe,
        ["System.Collections.Generic.IReadOnlyCollection`1::get_Count"] = CatalogEntry.Safe,
        ["System.Collections.Generic.IReadOnlyCollection`1::GetEnumerator"] = CatalogEntry.Safe,
        ["System.Collections.Generic.IReadOnlyDictionary`2::get_Count"] = CatalogEntry.Safe,
        ["System.Collections.Generic.IReadOnlyDictionary`2::ContainsKey"] = CatalogEntry.Safe,
        ["System.Collections.Generic.IReadOnlyDictionary`2::TryGetValue"] = CatalogEntry.Safe,
        ["System.Collections.Generic.IReadOnlyDictionary`2::get_Item"] = CatalogEntry.Safe,
        ["System.Collections.Generic.IReadOnlyDictionary`2::get_Keys"] = CatalogEntry.Safe,
        ["System.Collections.Generic.IReadOnlyDictionary`2::get_Values"] = CatalogEntry.Safe,
        ["System.Collections.Generic.IEnumerable`1::GetEnumerator"] = CatalogEntry.Safe,
        // ImmutableArray<T>（实测位于 global namespace，键按 MetadataName）
        ["System.Collections.Immutable.ImmutableArray`1::get_Length"] = CatalogEntry.Safe,
        ["System.Collections.Immutable.ImmutableArray`1::get_Item"] = CatalogEntry.Safe,
        ["System.Collections.Immutable.ImmutableArray`1::GetEnumerator"] = CatalogEntry.Safe,
        ["System.Collections.Immutable.ImmutableArray`1::IsDefault"] = CatalogEntry.Safe,
        ["System.Collections.Immutable.ImmutableArray`1::Contains"] = CatalogEntry.Safe,
        ["System.Collections.Immutable.ImmutableArray`1::IndexOf"] = CatalogEntry.Safe,
        ["System.Collections.Immutable.ImmutableArray::ToImmutableArray"] = CatalogEntry.Safe,
        ["System.Collections.Immutable.ImmutableList`1::get_Count"] = CatalogEntry.Safe,
        ["System.Collections.Immutable.ImmutableList`1::get_Item"] = CatalogEntry.Safe,
        ["System.Collections.Immutable.ImmutableList`1::Contains"] = CatalogEntry.Safe,
        ["System.Collections.Immutable.ImmutableList`1::GetEnumerator"] = CatalogEntry.Safe,
        ["System.Collections.Immutable.ImmutableDictionary`2::get_Count"] = CatalogEntry.Safe,
        ["System.Collections.Immutable.ImmutableDictionary`2::ContainsKey"] = CatalogEntry.Safe,
        ["System.Collections.Immutable.ImmutableDictionary`2::TryGetValue"] = CatalogEntry.Safe,
        ["System.Collections.Immutable.ImmutableDictionary`2::get_Item"] = CatalogEntry.Safe,
        ["System.Collections.Immutable.ImmutableHashSet`1::get_Count"] = CatalogEntry.Safe,
        ["System.Collections.Immutable.ImmutableHashSet`1::Contains"] = CatalogEntry.Safe,
        // 数组：读属性与元素（数组入口本身仍被 EBC2003 拒——那是入口稳定性立场）
        ["System.Array::get_Rank"] = CatalogEntry.Safe,
        ["System.Array::Clone"] = CatalogEntry.Safe,
        ["System.Array::GetEnumerator"] = CatalogEntry.Safe,

        // ── 高频字符串/数值成员（审计 FP8/FP9：金额与校验代码的骨干，此前全 Unknown）──
        ["System.String::IsNullOrWhiteSpace"] = CatalogEntry.Safe,
        ["System.String::IsNullOrEmpty"] = CatalogEntry.Safe,
        ["System.String::Trim"] = CatalogEntry.Safe,
        ["System.String::TrimStart"] = CatalogEntry.Safe,
        ["System.String::TrimEnd"] = CatalogEntry.Safe,
        ["System.String::StartsWith"] = CatalogEntry.Safe,
        ["System.String::EndsWith"] = CatalogEntry.Safe,
        ["System.String::IndexOf"] = CatalogEntry.Safe,
        ["System.String::Replace"] = CatalogEntry.Safe,
        ["System.String::Split"] = CatalogEntry.Safe,
        ["System.Decimal::Round"] = CatalogEntry.Safe,
        ["System.Decimal::Truncate"] = CatalogEntry.Safe,
        ["System.Decimal::Abs"] = CatalogEntry.Safe,
        ["System.Decimal::Compare"] = CatalogEntry.Safe,
        ["System.Decimal::GetHashCode"] = CatalogEntry.Safe,
        // 注意：decimal.TryParse/Parse 的**默认文化**重载受 CurrentCulture 影响，
        // 显式传 CultureInfo 的重载由调用点按实参判定（见 SummaryBuilder 的 cultureArgIsInvariant 处理）。

        // ── 日常 LINQ/数值/时间成员（第三轮用户视角审计 MAJOR-9：这些是惯用代码的骨干）──
        ["System.Linq.Enumerable::OrderBy"] = CatalogEntry.Safe,      // 显式比较器形态由调用点放行；默认比较器见下方说明
        ["System.Linq.Enumerable::OrderByDescending"] = CatalogEntry.Safe,
        ["System.Linq.Enumerable::ThenBy"] = CatalogEntry.Safe,
        ["System.Linq.Enumerable::ThenByDescending"] = CatalogEntry.Safe,
        ["System.Linq.Enumerable::GroupBy"] = CatalogEntry.Safe,
        ["System.Decimal::CompareTo"] = CatalogEntry.Safe,
        ["System.Decimal::Equals"] = CatalogEntry.Safe,
        ["System.DateTime::CompareTo"] = CatalogEntry.Safe,
        ["System.DateTimeOffset::CompareTo"] = CatalogEntry.Safe,
        ["System.DateOnly::AddDays"] = CatalogEntry.Safe,
        ["System.DateOnly::AddMonths"] = CatalogEntry.Safe,
        ["System.DateOnly::AddYears"] = CatalogEntry.Safe,
        ["System.DateOnly::CompareTo"] = CatalogEntry.Safe,
        ["System.DateOnly::FromDateTime"] = CatalogEntry.Safe,
        ["System.DateOnly::get_Year"] = CatalogEntry.Safe,
        ["System.DateOnly::get_Month"] = CatalogEntry.Safe,
        ["System.DateOnly::get_Day"] = CatalogEntry.Safe,
        ["System.TimeOnly::get_Hour"] = CatalogEntry.Safe,
        ["System.TimeOnly::get_Minute"] = CatalogEntry.Safe,
        ["System.DateTimeOffset::get_Year"] = CatalogEntry.Safe,
        ["System.DateTimeOffset::get_Month"] = CatalogEntry.Safe,
        ["System.DateTimeOffset::get_Day"] = CatalogEntry.Safe,
        ["System.DateTimeOffset::get_Hour"] = CatalogEntry.Safe,
        ["System.DateTimeOffset::get_Minute"] = CatalogEntry.Safe,
        ["System.DateTimeOffset::get_Second"] = CatalogEntry.Safe,
        ["System.DateTime::get_Year"] = CatalogEntry.Safe,
        ["System.DateTime::get_Month"] = CatalogEntry.Safe,
        ["System.DateTime::get_Day"] = CatalogEntry.Safe,
        ["System.DateTime::get_Hour"] = CatalogEntry.Safe,
        ["System.DateTime::get_Minute"] = CatalogEntry.Safe,
        ["System.DateTime::get_Second"] = CatalogEntry.Safe,
        ["System.DateTimeOffset::AddDays"] = CatalogEntry.Safe,
        ["System.DateTimeOffset::AddHours"] = CatalogEntry.Safe,
        ["System.DateTimeOffset::AddMinutes"] = CatalogEntry.Safe,
        ["System.DateTimeOffset::get_UtcDateTime"] = CatalogEntry.Safe,
        ["System.DateTimeOffset::get_Date"] = CatalogEntry.Safe,
        ["System.Array::Empty"] = CatalogEntry.Safe,
        ["System.Collections.Generic.KeyValuePair`2::get_Key"] = CatalogEntry.Safe,
        ["System.Collections.Generic.KeyValuePair`2::get_Value"] = CatalogEntry.Safe,
        ["System.String::Compare"] = CatalogEntry.Safe,     // 显式 StringComparison 形态；默认文化形态由调用点/目录边界说明

        // ── 明确确定性/文化无关的实例（审计 FP5/FP7：调用点按实参判定后的放行目标）──
        ["System.StringComparer::get_Ordinal"] = CatalogEntry.Safe,
        ["System.StringComparer::get_OrdinalIgnoreCase"] = CatalogEntry.Safe,
        ["System.StringComparer::get_InvariantCulture"] = CatalogEntry.Safe,
        ["System.StringComparer::get_InvariantCultureIgnoreCase"] = CatalogEntry.Safe,
        ["System.Decimal::Parse"] = CatalogEntry.WithHidden(HiddenInputKind.Culture),

        // ── Frozen 容器成员（审计第十轮 FP7 后半：容器已入不可变白名单，成员也要有据）──
        ["System.Linq.Enumerable::ToFrozenSet"] = CatalogEntry.Safe,
        ["System.Linq.Enumerable::ToFrozenDictionary"] = CatalogEntry.Safe,
        // 实测：`items.ToFrozenSet()` 绑定到 FrozenSet 的工厂成员，属主是 FrozenSet 本身。
        ["System.Collections.Frozen.FrozenSet::ToFrozenSet"] = CatalogEntry.Safe,
        ["System.Collections.Frozen.FrozenDictionary::ToFrozenDictionary"] = CatalogEntry.Safe,
        // object.GetType / 反射成员名读取（审计第十轮 E7）。
        ["System.Object::GetType"] = CatalogEntry.Safe,
        ["System.Reflection.MemberInfo::get_Name"] = CatalogEntry.Safe,
        ["System.Type::get_Name"] = CatalogEntry.Safe,
        // Dictionary 值/键集合的枚举器（审计第十轮 D19：Values foreach 曾 Unknown）。
        ["System.Collections.Generic.Dictionary`2+ValueCollection::GetEnumerator"] = CatalogEntry.Safe,
        ["System.Collections.Generic.Dictionary`2+KeyCollection::GetEnumerator"] = CatalogEntry.Safe,
        ["System.Collections.Generic.Dictionary`2+ValueCollection::get_Count"] = CatalogEntry.Safe,
        ["System.Collections.Generic.Dictionary`2+KeyCollection::get_Count"] = CatalogEntry.Safe,
        ["System.Collections.Frozen.FrozenSet`1::Contains"] = CatalogEntry.Safe,
        ["System.Collections.Frozen.FrozenSet`1::get_Count"] = CatalogEntry.Safe,
        ["System.Collections.Frozen.FrozenDictionary`2::ContainsKey"] = CatalogEntry.Safe,
        ["System.Collections.Frozen.FrozenDictionary`2::get_Count"] = CatalogEntry.Safe,
        ["System.Collections.Frozen.FrozenDictionary`2::TryGetValue"] = CatalogEntry.Safe,

        // ── 文化敏感的格式化（审计第三轮 F12）──
        // 插值与 string.Format 使用当前文化 ⇒ 相同输入在不同机器/区域下结果不同。
        ["System.String::Format"] = CatalogEntry.WithHidden(HiddenInputKind.Culture),
        ["System.String::ToLower"] = CatalogEntry.WithHidden(HiddenInputKind.Culture),
        ["System.String::ToUpper"] = CatalogEntry.WithHidden(HiddenInputKind.Culture),
        ["System.IFormattable::ToString"] = CatalogEntry.WithHidden(HiddenInputKind.Culture),
        ["System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted"]
            = CatalogEntry.WithHidden(HiddenInputKind.Culture),

        // ── 非确定来源：显式标注为不安全的哈希/身份 ──
        ["System.Object::GetHashCode"] = CatalogEntry.WithHidden(HiddenInputKind.Ambient),
        ["System.Runtime.CompilerServices.RuntimeHelpers::GetHashCode"] = CatalogEntry.WithHidden(HiddenInputKind.Ambient),
    };

    /// <summary>按精确符号查目录。未命中返回 false（调用方保守处理，不默认 Safe）。</summary>
    public static bool TryLookup(string typeFullName, string memberName, out CatalogEntry entry)
    {
        if (Entries.TryGetValue(typeFullName + "::" + memberName, out entry)) return true;
        // 部分类为嵌套/泛型形态（List`1），调用方传入已归一化的类型全名。
        entry = default;
        return false;
    }

    /// <summary>已知可变集合的"修改接收者自身"的成员。
    /// 键 = 类型规范名 + "::" + 成员名；用于判定"修改参数/receiver 的可变集合"是否越界。</summary>
    private static readonly HashSet<string> KnownMutators = new()
    {
        "System.Collections.Generic.List`1.Add",
        "System.Collections.Generic.List`1.AddRange",
        "System.Collections.Generic.List`1.Insert",
        "System.Collections.Generic.List`1.Remove",
        "System.Collections.Generic.List`1.RemoveAt",
        "System.Collections.Generic.List`1.Clear",
        "System.Collections.Generic.List`1.Sort",
        "System.Collections.Generic.Dictionary`2.Add",
        "System.Collections.Generic.Dictionary`2.Remove",
        "System.Collections.Generic.Dictionary`2.Clear",
        "System.Collections.Generic.HashSet`1.Add",
        "System.Collections.Generic.HashSet`1.Remove",
        "System.Collections.Generic.HashSet`1.Clear",
        "System.Collections.Generic.Queue`1.Enqueue",
        "System.Collections.Generic.Stack`1.Push",
        "System.Text.StringBuilder.Append",
        "System.Text.StringBuilder.Clear",
    };

    /// <summary>该成员是否为已知可变集合修改器（修改 receiver / 参数自身）。</summary>
    public static bool IsKnownMutator(string typeFullName, string memberName)
        => KnownMutators.Contains(typeFullName + "." + memberName);


}
