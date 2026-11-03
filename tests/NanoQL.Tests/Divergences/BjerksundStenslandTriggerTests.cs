// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Bjerksund-Stensland where its 1993 trigger falls below its lower bound B₀. Where bT + 2√v &lt; 0 the trigger's
/// exponent is positive and I falls below B₀ (<c>bjerksundstenslandengine.cpp:196-206</c>): an infeasible exercise
/// boundary, on which QuantLib's closed form falls below its European value, which its floor then returns, up to 3.4
/// below its own FD engine on its own Greeks case's inputs. nanoql puts the trigger at the perpetual boundary B∞ there:
/// a flat boundary at or above B₀ is a feasible exercise rule, so its value is a lower bound of the American (Bjerksund
/// and Stensland 2002, §3). QuantLib's check: its QD+ engine, which the trigger's value stays below and within 0.5% of,
/// and its FD engine (1,000 × 1,000) recorded (QuantLib-Python 1.44, on <c>TestBjerksundStenslandAmericanGreeks</c>'
/// market: q on Act/360, r on Act/365 (Fixed), the volatility on 30/360).
/// </summary>
public class BjerksundStenslandTriggerTests
{
    private static readonly Date Today = new(5, Month.December, 2022);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    // (type, spot, volatility, q, r, QuantLib 1.44, its FD engine, its QD+), five years, a strike of 100
    public static TheoryData<OptionType, double, double, double, double, double, double, double> Region => new()
    {
        { OptionType.Call, 99.9, 0.1, 0.12, 0.0, 0.015971820907886847, 1.458381288388452, 1.448598085041496 },
        { OptionType.Call, 99.9, 0.1, 0.12, 0.02, 0.06083192366094156, 1.7330535501250404, 1.7144773222905232 },
        { OptionType.Put, 99.9, 0.1, 0.0, 0.1, 0.09999999999999432, 1.8224764052922247, 1.8318340874966306 },
        { OptionType.Put, 110.0, 0.1, 0.0, 0.1, 0.021715692353158844, 0.2622902815899526, 0.2634541637609968 },
        { OptionType.Put, 99.9, 0.1, 0.0, 0.2, 0.09999999999999432, 0.9355567733849425, 0.9460606070713021 },
        { OptionType.Put, 110.0, 0.1, 0.0, 0.2, 1.2877850575226076e-06, 0.019840286739700626, 0.0201024180528611 },
        { OptionType.Put, 99.9, 0.2, 0.0, 0.2, 0.11762752391579945, 3.525374258787788, 3.5412471827794114 },
        { OptionType.Put, 110.0, 0.2, 0.0, 0.2, 0.0651448945415748, 1.3413635154617005, 1.3470832760510238 },
        { OptionType.Put, 150.0, 0.2, 0.0, 0.2, 0.007410122477885639, 0.057983749521873885, 0.05818427382440138 },
        { OptionType.Put, 99.9, 0.1, 0.08, 0.2, 0.09999999999999432, 1.4945579172893182, 1.5166571143226553 },
        { OptionType.Put, 110.0, 0.1, 0.08, 0.2, 0.0032516947261298453, 0.13911352513469316, 0.14404885402903442 },
    };

    [Theory]
    [MemberData(nameof(Region))]
    public void BelowB0ThePerpetualTriggerGivesALowerBound(
        OptionType type, double spot, double vol, double q, double r, double quantLib, double fd, double qdPlus)
    {
        _ = quantLib;
        _ = fd;
        double value = Price(type, spot, vol, q, r, 1825).NPV!.Value;
        Assert.True(value <= qdPlus && value >= 0.995 * qdPlus, $"{value} against QuantLib's QD+ {qdPlus}");
    }

    [Theory]
    [InlineData(60.0)] // QuantLib's FD engine 40.0000000000091, its QD+ 40.00238814294711
    [InlineData(70.0)] // 30.000000000003478 and 30.00292561365572
    [InlineData(80.0)] // 20.00000000000046 and 20.00413124553608
    public void AtOrAboveTheBoundaryTheOptionIsExercised(double spot)
    {
        // the put of five years at r 20%, q 0, volatility 10%, in the region, with the spot past B∞ in the call
        // frame: the exercise value, as QuantLib's
        Assert.Equal(100.0 - spot, Price(OptionType.Put, spot, 0.1, 0.0, 0.2, 1825).NPV!.Value);
    }

    [Fact]
    public void TheValueJumpsAtTheRegionsEdge()
    {
        // the put of a year at r 20%, q 0, S 99.9, where bT + 2√v = 0 at σ 10%: at and above it QuantLib's trigger,
        // whose value is the exercise value 0.1 at σ 10.001% (QuantLib's FD engine 0.9390253994764394, its QD+
        // 0.942693557798808) and 0.636 at σ 10.5% (FD 1.028); below it B∞'s, at σ 9.999% within 0.5% under QD+'s
        // 0.9423442564695695 (FD 0.938674651163587), where QuantLib's value is 0.1 again
        Assert.Equal(0.09999999999999432, Price(OptionType.Put, 99.9, 0.10001, 0.0, 0.2, 365).NPV!.Value);
        double below = Price(OptionType.Put, 99.9, 0.09999, 0.0, 0.2, 365).NPV!.Value;
        Assert.True(below <= 0.9423442564695695 && below >= 0.995 * 0.9423442564695695, $"{below}");
    }

    [Theory]
    [InlineData(0.30, 0.05, 0.05, 0.1832994549239894, 0.18342662954690842)] // QuantLib 2.231657033640619e-57
    [InlineData(0.10, 0.0, 0.02, 0.07350239807437914, 0.07355383123648895)] // QuantLib 3.123824985007703e-57
    public void FarBelowTheForwardThePerpetualTriggerIsStillPriced(double r, double q, double vol, double bound, double qdPlus)
    {
        // ten-year puts at 100 on a spot of 100 on Act/365 (Fixed), where QuantLib's trigger sits more than 12.5
        // standard deviations above the forward and its shortcut returns the European value, about 0: at B∞ the value
        // is at most the perpetual put's, (K − S*)(S/S*)^λ, the same flat trigger held forever, and within 1e-6 of it;
        // QuantLib's QD+ recorded
        _ = qdPlus;
        var today = new Date(15, Month.October, 2026);
        var a365 = new Actual365Fixed();
        double value = BjerksundStenslandApproximationEngine.Calculate(
            new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 100.0), new AmericanExercise(today, today + 3650)),
            new EvaluationContext { EvaluationDate = today },
            new BlackScholesMertonProcess(
                100.0,
                new FlatForward(today, q, a365),
                new FlatForward(today, r, a365),
                new BlackConstantVol(today, new NullCalendar(), vol, a365))).NPV!.Value;
        Assert.True(value <= bound * (1 + 1e-14) && value >= bound * (1 - 1e-6), $"{value} against the perpetual {bound}");
    }

    private static OneAssetOptionResults Price(OptionType type, double spot, double vol, double q, double r, int days) =>
        BjerksundStenslandApproximationEngine.Calculate(
            new VanillaOption(new PlainVanillaPayoff(type, 100.0), new AmericanExercise(Today, Today + days)),
            Context,
            new BlackScholesMertonProcess(
                spot,
                new FlatForward(Today, q, new Actual360()),
                new FlatForward(Today, r, new Actual365Fixed()),
                new BlackConstantVol(Today, new NullCalendar(), vol, new Thirty360(Thirty360Convention.ISDA))));
}
