// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The shout, swing and CIR engines on structures dated differently (the mismatched-dates class; master Revision 58).
/// QuantLib's engines run on the risk-free curve's times and read the dividend curve and the volatility at those times
/// on their own clocks (<c>fdblackscholesshoutengine.cpp:65-73</c>, <c>fdsimplebsswingengine.cpp:47-77</c>,
/// <c>fdcirvanillaengine.cpp:64-69</c>): on a risk-free curve settling two business days after the other structures,
/// QuantLib's shout engine reads 14.261505884225283 and its swing engine 17.000771278512975 [executed on the wheel:
/// <c>scratchpad\c4\wheel_mismatched.py</c>]. nanoql's run on <see cref="FdmBlackScholesOp.Clock"/>, from the earliest
/// reference date, each structure accruing nothing before its own, as its finite-difference Black-Scholes engine does
/// (<c>Divergences/FdmMismatchedTimesTests.cs</c>). QuantLib's check: its own engines on the market restated from
/// today, the risk-free curve discounting nothing before its date; for the CIR engine, whose QuantLib values are
/// another model's (<c>Divergences/FdCirModelTests.cs</c>), nanoql's own engine on that market.
/// </summary>
public class FdShoutSwingCirMismatchedTimesTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void TheShoutEnginePricesTheRestatedMarket()
    {
        // a year's shout put at 105 on 100, q 2%, σ 25%; QuantLib's engine on the restated market
        var shout = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 105.0), new AmericanExercise(Today, Today + 365));
        double value = FdBlackScholesShoutEngine.Calculate(shout, Context, Mixed(), tGrid: 100, xGrid: 200).NPV!.Value;
        Assert.Equal(14.347631521119313, value, 1e-10);
    }

    [Fact]
    public void TheSwingEnginePricesTheRestatedMarket()
    {
        // three rights on six monthly forward puts at 100; QuantLib's engine on the restated market
        var swing = new VanillaSwingOption(
            new VanillaForwardPayoff(OptionType.Put, 100.0),
            new SwingExercise(Enumerable.Range(1, 6).Select(i => Today + (30 * i))),
            0,
            3);
        double value = FdSimpleBSSwingEngine.Calculate(swing, Context, Mixed(), 50, 200).NPV!.Value;
        Assert.Equal(17.295329017592557, value, 1e-10);
    }

    [Fact]
    public void TheCirEnginePricesTheRestatedMarket()
    {
        // a year's put at 105 on 100 under a CIR rate (κ 2, σ 0.1, θ = r₀ = 5%, ρ 0.3), the equity's curves as above
        var put = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 105.0), new EuropeanExercise(Today + 365));
        var cir = new CoxIngersollRossProcess(2.0, 0.1, 0.05, 0.05);
        double mixed = FdCIRVanillaEngine.Calculate(put, Context, cir, Mixed(), 50, 100, 50, 0, 0.3).NPV!.Value;
        double restated = FdCIRVanillaEngine.Calculate(put, Context, cir, Restated(), 50, 100, 50, 0, 0.3).NPV!.Value;
        Assert.Equal(restated, mixed, 1e-9);
    }

    // the risk-free curve settling two TARGET days on, the dividend curve and the volatility from today
    private static BlackScholesMertonProcess Mixed()
    {
        YieldTermStructure settling = FlatForward.Create(2, new TARGET(), 0.05, A365, Context);
        Assert.Equal(Today + 4, settling.ReferenceDate);
        return new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02, A365), settling, new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));
    }

    // the same market from today: the risk-free curve's discount 1 up to its date, log-linear after
    private static BlackScholesMertonProcess Restated()
    {
        Date settle = Today + 4, far = Today + 3650;
        var restated = new DiscountCurve([Today, settle, far], [1.0, 1.0, Math.Exp(-0.05 * A365.YearFraction(settle, far))], A365);
        return new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02, A365), restated, new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));
    }
}
