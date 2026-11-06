using EvoOffer.Services;

namespace EvoOffer.Models;

public enum DiscountValueType { Percentage, Absolute }
public enum DiscountScope { PerUnit, ItemTotal }

/// <summary>A discount on VAT-inclusive amounts, retained when quantity or prices change.</summary>
public sealed record DiscountDefinition(DiscountScope Scope, DiscountValueType ValueType, decimal Value)
{
    public bool IsValid => Enum.IsDefined(Scope) && Enum.IsDefined(ValueType)
        && Value >= 0m && decimal.Round(Value, 2) == Value
        && (ValueType != DiscountValueType.Percentage || Value <= 100m);

    /// <param name="baseAmount">The undiscounted gross unit or item/offer total selected by the scope.</param>
    public static bool TryCreate(DiscountScope scope, DiscountValueType valueType, string? valueText,
        decimal baseAmount, out DiscountDefinition? discount, out string error)
    {
        discount = null;
        error = string.Empty;
        if (!Enum.IsDefined(scope) || !Enum.IsDefined(valueType)
            || !ServiceDraft.TryParsePrice(valueText, out var value)
            || (valueType == DiscountValueType.Percentage && value > 100m))
        {
            error = LocalizationService.Get(valueType == DiscountValueType.Percentage
                ? "DiscountPercentageValidation" : "DiscountAbsoluteValidation");
            return false;
        }

        if (baseAmount < 0m || (valueType == DiscountValueType.Absolute && value > baseAmount))
        {
            error = LocalizationService.Format("DiscountExceedsBaseValidation",
                Math.Max(0m, baseAmount).ToString("N2", LocalizationService.Culture));
            return false;
        }

        if (value > 0m)
            discount = new DiscountDefinition(scope, valueType, value);
        return true;
    }

    public decimal CalculateAmount(decimal grossTotal, decimal quantity = 1m)
    {
        if (!IsValid)
            throw new ArgumentOutOfRangeException(nameof(Value));
        if (grossTotal < 0m)
            throw new ArgumentOutOfRangeException(nameof(grossTotal));
        if (quantity <= 0m)
            throw new ArgumentOutOfRangeException(nameof(quantity));

        // Divide first for percentages and compare before multiplying a unit
        // amount, so even a previously valid discount cannot overflow on edits.
        var amount = ValueType == DiscountValueType.Percentage
            ? grossTotal * (Value / 100m)
            : Scope == DiscountScope.PerUnit
                ? Value >= grossTotal / quantity ? grossTotal : Value * quantity
                : Math.Min(Value, grossTotal);
        return Math.Min(grossTotal, decimal.Round(amount, 2, MidpointRounding.AwayFromZero));
    }

    public string DescribeValue(string? language = null)
    {
        var culture = LocalizationService.CultureFor(language ?? LocalizationService.Language);
        return ValueType == DiscountValueType.Percentage
            ? $"{Value.ToString("0.##", culture)}%"
            : $"{Value.ToString("N2", culture)} RON";
    }

    public string Describe(string? language = null)
    {
        language ??= LocalizationService.Language;
        return string.Format(LocalizationService.CultureFor(language),
            LocalizationService.Get(Scope == DiscountScope.PerUnit
                ? "DiscountPerUnitDescription" : "DiscountItemTotalDescription", language),
            DescribeValue(language));
    }
}
