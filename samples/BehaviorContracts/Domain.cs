// Domain.cs — 无 Godot 示例：展示合法与违规业务类型（不依赖资源 Claim/Runtime）。
using System;
using System.Collections.Generic;
using EffectLedger.Contracts;

namespace BehaviorContractsSample;

// ── 合法：不可变值 ──
public sealed class OrderSnapshot : IConstrained<ImmutableValue>
{
    private readonly decimal _subtotal;
    private readonly IReadOnlyList<LineItem> _lines;

    public OrderSnapshot(decimal subtotal, IReadOnlyList<LineItem> lines)
    {
        _subtotal = subtotal;
        // 文档规则："IReadOnlyList 包装不算冻结"——持有外部集合时必须防御性拷贝，
        // 否则调用方仍可通过原引用修改我们声称不可变的数据。
        _lines = lines.ToArray();
    }

    public decimal Subtotal => _subtotal;
    public IReadOnlyList<LineItem> Lines => _lines;
}

public sealed class LineItem : IConstrained<ImmutableValue>
{
    private readonly string _product;
    private readonly int _quantity;
    public LineItem(string product, int quantity) { _product = product; _quantity = quantity; }
    public string Product => _product;
    public int Quantity => _quantity;
}

// ── 合法：确定性计算（显式输入，局部集合，无隐藏依赖）──
public sealed class PriceCalculator : IConstrained<DeterministicComputation>
{
    public decimal Calculate(OrderSnapshot order, PricingRules rules)
    {
        var total = order.Subtotal;
        var list = new List<decimal>(); // 不逃逸的局部集合
        foreach (var line in order.Lines)
            list.Add(line.Quantity * 1.0m);
        return total * rules.Multiplier;
    }
}

public sealed class PricingRules : IConstrained<ImmutableValue>
{
    private readonly decimal _multiplier;
    public PricingRules(decimal multiplier) { _multiplier = multiplier; }
    public decimal Multiplier => _multiplier;
}
