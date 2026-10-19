// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The swaption enums' texts and integers, each port's check of QuantLib's stream operators on its enums (playbook §2):
/// <c>Settlement::Type</c> streams <see cref="SettlementType.Physical"/> as "Delivery" (<c>swaption.cpp:106-116</c>),
/// so its text is <see cref="SwaptionFunctions.ToString(SettlementType)"/>, which refuses a value outside the enum
/// with QuantLib's "unknown Settlement::Type(n)"; <c>Settlement::Method</c> streams its member names
/// (<c>:118-131</c>), which <see cref="object.ToString"/> gives; <c>Swaption::PriceType</c> and the engines'
/// <c>CashAnnuityModel</c> have no stream operator. The integers are QuantLib's, and no two names share a value.
/// </summary>
public class SettlementTypeTextTests
{
    [Fact]
    public void TheSettlementEnumsGiveQuantLibsTexts()
    {
        Assert.Equal("Delivery", SwaptionFunctions.ToString(SettlementType.Physical));
        Assert.Equal("Cash", SwaptionFunctions.ToString(SettlementType.Cash));
        ArgumentOutOfRangeException e =
            Assert.Throws<ArgumentOutOfRangeException>(() => SwaptionFunctions.ToString((SettlementType)2));
        Assert.Equal("type", e.ParamName);
        Assert.StartsWith("unknown Settlement::Type(2)", e.Message, StringComparison.Ordinal);

        Assert.Equal("PhysicalOTC", SettlementMethod.PhysicalOTC.ToString());
        Assert.Equal("PhysicalCleared", SettlementMethod.PhysicalCleared.ToString());
        Assert.Equal("CollateralizedCashPrice", SettlementMethod.CollateralizedCashPrice.ToString());
        Assert.Equal("ParYieldCurve", SettlementMethod.ParYieldCurve.ToString());

        AssertMembers<SettlementType>(new() { ["Physical"] = 0, ["Cash"] = 1 });
        AssertMembers<SettlementMethod>(new()
        {
            ["PhysicalOTC"] = 0, ["PhysicalCleared"] = 1, ["CollateralizedCashPrice"] = 2, ["ParYieldCurve"] = 3,
        });
        AssertMembers<SwaptionPriceType>(new() { ["Spot"] = 0, ["Forward"] = 1 });
        AssertMembers<CashAnnuityModel>(new() { ["SwapRate"] = 0, ["DiscountCurve"] = 1 });
    }

    // The enum's names and integers are exactly QuantLib's, and no two names share a value.
    private static void AssertMembers<T>(Dictionary<string, int> expected)
        where T : struct, Enum
    {
        string[] names = Enum.GetNames<T>();
        Assert.Equal(expected.Keys.Order(), names.Order());
        foreach (string name in names)
        {
            Assert.Equal(expected[name], Convert.ToInt32(Enum.Parse<T>(name), System.Globalization.CultureInfo.InvariantCulture));
        }

        Assert.Equal(names.Length, Enum.GetValues<T>().Distinct().Count());
    }
}
