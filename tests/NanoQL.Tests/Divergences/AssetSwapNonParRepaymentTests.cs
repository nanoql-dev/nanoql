// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The asset swap's final repayment: QuantLib pays a given non-par repayment as an amount
/// (<c>assetswap.cpp:125-134</c>), while <c>fairNonParRepayment</c> reads it per 100 of the bond's notional
/// (<c>:284-296</c>), and with none given it repays the bond's redemption but stores 100 (<c>:130</c>). On the wheel, a
/// 7-year 4% bond issued 15 March 2024 on a flat 3% curve, swapped at its clean price on Euribor 6M on 23 October 2026:
/// on a 1e6 face a given repayment of 100 pays 100, the NPV 876,253.3609798652 and the fair spread −0.21178102152342376
/// at par, where with none given both are 0; its fair repayment (QuantLib's formula on the wheel's results, which hide
/// the member), 199.99 with 100 given and 100 with none, fed back leaves 876,165.7356437672 and 876,253.3609798652; on
/// a face of 100 redeemed at 101 the fair repayment is 99.99999999999999, which fed back leaves 0.8763409950793744.
/// nanoql pays a given repayment as that share of the notional, and with none given stores the redemption per 100 of
/// notional, 101 here. QuantLib's check: its fair repayment fed back gives an NPV of 0 only on the consistent reading;
/// on a face of 100 redeemed at 100 the two agree, and QuantLib's cases keep their bits.
/// </summary>
public class AssetSwapNonParRepaymentTests
{
    private static readonly Date Today = new(23, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.03, new Actual365Fixed());
    private static readonly IborIndex Index = new Euribor6M(Curve);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AGivenRepaymentIsPerHundredOfTheNotional(bool parSwap)
    {
        Bond bond = BondOf(1e6, 100.0);
        double clean = CleanPrice(bond);
        AssetSwapResults given = Price(Swap(bond, clean, parSwap, 100.0));
        AssetSwapResults none = Price(Swap(bond, clean, parSwap, null));
        Assert.True(Math.Abs(given.NPV!.Value - none.NPV!.Value) <= 1e-6, $"{given.NPV} {none.NPV}");
        Assert.True(Math.Abs(AssetSwap.FairSpread(given) - AssetSwap.FairSpread(none)) <= 1e-15);
    }

    [Theory]
    [InlineData(1e6, null)]
    [InlineData(1e6, 100.0)]
    [InlineData(1e6, 120.0)]
    public void TheFairRepaymentFedBackIsAtPar(double face, double? given)
    {
        Bond bond = BondOf(face, 100.0);
        double clean = CleanPrice(bond);
        double fair = AssetSwap.FairNonParRepayment(Price(Swap(bond, clean, true, given)));
        double npv = Price(Swap(bond, clean, true, fair)).NPV!.Value;
        Assert.True(Math.Abs(npv) <= 1e-6, $"{fair} {npv}");
    }

    [Fact]
    public void ARedemptionAboveParIsTheStoredRepayment()
    {
        Bond bond = BondOf(100.0, 101.0);
        double clean = CleanPrice(bond);
        AssetSwap swap = Swap(bond, clean, true, null);
        Assert.Equal(101.0, swap.NonParRepayment, 12);
        double fair = AssetSwap.FairNonParRepayment(Price(swap));
        Assert.True(Math.Abs(fair - 101.0) <= 1e-12, $"{fair}");
        Assert.True(Math.Abs(Price(Swap(bond, clean, true, fair)).NPV!.Value) <= 1e-12);
    }

    // A 7-year 4% bond issued 15 March 2024, annual, unadjusted on TARGET, Actual/Actual (ISMA).
    private static Bond BondOf(double face, double redemption)
    {
        var issue = new Date(15, Month.March, 2024);
        var schedule = new Schedule(
            issue, new Date(15, Month.March, 2031), new Period(1, TimeUnit.Years), new TARGET(), BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted, DateGenerationRule.Backward, false, Context);
        return new FixedRateBond(
            2, face, schedule, [0.04], new ActualActual(ActualActualConvention.ISMA), BusinessDayConvention.Following,
            redemption, issue);
    }

    private static double CleanPrice(Bond bond) =>
        bond.CleanPrice(DiscountingBondEngine.Calculate(bond, Context, Curve), Context);

    private static AssetSwap Swap(Bond bond, double clean, bool parSwap, double? repayment) =>
        new(true, bond, clean, Index, 0.0, Context, null, new Actual360(), parSwap, 1.0, repayment);

    private static AssetSwapResults Price(AssetSwap swap) => DiscountingSwapEngine.Calculate(swap, Context, Curve);
}
