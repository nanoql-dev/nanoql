// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The irregular swap's conventions and two routes: on a swap engine its fair rate and fair spread are null, QuantLib's
/// own "result not available", where QuantLib's <c>fetchResults</c> sets a debug 0 (0 where the vanilla fair rate is
/// 0.0305); at constant notionals and rates it prices as the vanilla swap, leg by leg; and Hagan's engine on its
/// regular swaption prices as the Bachelier engine within its bisection's 1e-8 on λ, 5.1e-7 relative, QuantLib's prices
/// to 12 digits.
/// </summary>
public class IrregularSwapTests
{
    private static readonly Date Today = new(15, Month.June, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.03, new Actual365Fixed());

    [Fact]
    public void OnASwapEngineTheFairRateAndSpreadAreNull()
    {
        VanillaSwap vanilla = Vanilla(0.03, Today + new Period(2, TimeUnit.Days));
        var swap = new IrregularSwap(vanilla.Type, vanilla.FixedLeg, vanilla.FloatingLeg);
        var results = (IrregularSwapResults)DiscountingSwapEngine.Calculate(swap, Context, Curve);
        Assert.Null(results.FairRate);
        Assert.Null(results.FairSpread);
        Assert.NotNull(results.FixedLegBPS);
        Assert.NotNull(results.FloatingLegBPS);
    }

    [Fact]
    public void AtConstantTermsItPricesAsTheVanillaSwap()
    {
        VanillaSwap vanilla = Vanilla(0.03, Today + new Period(2, TimeUnit.Days));
        var swap = new IrregularSwap(vanilla.Type, vanilla.FixedLeg, vanilla.FloatingLeg);
        SwapResults irregular = DiscountingSwapEngine.Calculate(swap, Context, Curve);
        FixedVsFloatingSwapResults plain = DiscountingSwapEngine.Calculate(vanilla, Context, Curve);
        Assert.Equal(plain.NPV, irregular.NPV);
        Assert.Equal(plain.LegNPV[0], irregular.LegNPV[0]);
        Assert.Equal(plain.LegNPV[1], irregular.LegNPV[1]);
        Assert.Equal(plain.LegBPS[0], irregular.LegBPS[0]);
    }

    // QuantLib 1.44's prices on this set-up
    [Theory]
    [InlineData(0.02, 0.0498539343948, 0.049853958473)]
    [InlineData(0.03, 0.0187725096698, 0.0187725191931)]
    [InlineData(0.04, 0.00404197072247, 0.00404197222292)]
    public void HagansEngineOnARegularSwapIsTheBachelierEngine(double strike, double quantLibHagan, double quantLibBachelier)
    {
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        YieldTermStructure curve = new FlatForward(today, 0.03, new Actual365Fixed());
        var vol = new ConstantSwaptionVolatility(
            today, new TARGET(), BusinessDayConvention.ModifiedFollowing, 0.01, new Actual365Fixed(), VolatilityType.Normal);
        Date start = new TARGET().Advance(today, 1, TimeUnit.Years);
        VanillaSwap vanilla = new MakeVanillaSwap(new Period(5, TimeUnit.Years), new Euribor6M(curve))
            .WithFixedRate(strike)
            .WithEffectiveDate(start)
            .WithType(SwapType.Payer)
            .ToVanillaSwap(context);
        var exercise = new EuropeanExercise(start);

        double hagan = HaganIrregularSwaptionEngine.Calculate(
            new IrregularSwaption(new IrregularSwap(SwapType.Payer, vanilla.FixedLeg, vanilla.FloatingLeg), exercise),
            context,
            vol,
            curve).NPV!.Value;
        double bachelier = BachelierSwaptionEngine.Calculate(new Swaption(vanilla, exercise), context, curve, vol).NPV!.Value;
        Assert.True(Math.Abs((hagan / bachelier) - 1) <= 6e-7, $"{hagan} for {bachelier}");
        Assert.True(Math.Abs(hagan - quantLibHagan) <= 1e-12);
        Assert.True(Math.Abs(bachelier - quantLibBachelier) <= 1e-12);
    }

    private static VanillaSwap Vanilla(double fixedRate, Date start) =>
        new MakeVanillaSwap(new Period(4, TimeUnit.Years), new Euribor6M(Curve))
            .WithFixedRate(fixedRate)
            .WithType(SwapType.Payer)
            .WithEffectiveDate(start)
            .WithDiscountingTermStructure(Curve)
            .ToVanillaSwap(Context);
}
