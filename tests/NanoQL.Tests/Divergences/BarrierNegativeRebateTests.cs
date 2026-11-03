// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Quotes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A negative rebate (fixed). QuantLib's analytic barrier engine pays a rebate only where <c>rebate() &gt; 0</c>
/// (<c>analyticbarrierengine.cpp:227</c>, <c>:244</c>), so a rebate of −3 prices as none: on a one-year call struck at
/// 100 (spot 100, r 5%, q 2%, volatility 25%) its four barrier types are 2.0 to 2.1 from QuantLib's finite-difference
/// engine, which prices the rebate (QuantLib-Python 1.44). nanoql tests <c>rebate != 0</c>, which leaves zero and
/// positive rebates on QuantLib's bits; the Vanna-Volga engine and the implied volatility, which price through the
/// analytic engine, follow. QuantLib's checks: its finite-difference engine, and the price's linearity in the rebate.
/// </summary>
public class BarrierNegativeRebateTests
{
    [Fact]
    public void ANegativeRebateIsPricedAsQuantLibsFiniteDifferenceEngineDoes()
    {
        var today = new Date(15, October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        var process = new BlackScholesMertonProcess(
            100.0,
            new FlatForward(today, 0.02, dc),
            new FlatForward(today, 0.05, dc),
            new BlackConstantVol(today, new NullCalendar(), 0.25, dc));
        var exercise = new EuropeanExercise(today + 365);
        double Npv(BarrierType type, double barrier, double rebate) => AnalyticBarrierEngine.Calculate(
            new BarrierOption(type, barrier, rebate, new PlainVanillaPayoff(OptionType.Call, 100.0), exercise),
            context,
            process).NPV!.Value;

        // (type, barrier, QuantLib's finite-difference engine at rebate −3 on a 400 × 800 grid, QuantLib's analytic
        // engine at rebates 0 and 3)
        (BarrierType Type, double Barrier, double Fd, double At0, double At3)[] cases =
        [
            (BarrierType.DownOut, 90.0, 6.142735571572747, 8.138810547624583, 10.13543119056153),
            (BarrierType.UpOut, 110.0, -2.0138525618281182, 0.06228236027281042, 2.1405990475826013),
            (BarrierType.DownIn, 90.0, 2.0566329461350934, 2.9849513804335537, 3.912826657196435),
            (BarrierType.UpIn, 110.0, 10.208098906876431, 11.061479567785327, 11.912781719846869),
        ];
        foreach (var c in cases)
        {
            // QuantLib: the rebate-free price, its rebate of −3 dropped
            double value = Npv(c.Type, c.Barrier, -3.0);
            Assert.True(Math.Abs(value - c.Fd) < 2.5e-3, $"{c.Type}: {value} against {c.Fd}");
            Assert.Equal(c.At0, Npv(c.Type, c.Barrier, 0.0));
            Assert.Equal(c.At3, Npv(c.Type, c.Barrier, 3.0));
            Assert.Equal((2.0 * c.At0) - c.At3, value, 1e-12);
        }
    }

    [Fact]
    public void AVannaVolgaKnockOutsNegativeRebateIsLinearThroughZero()
    {
        // an up-and-out call at 1.40, strike 1.25, a year, on a flat 12% smile (spot 1.30, domestic 3% and foreign 1%
        // Actual/360 rates on Actual/365 Fixed curves)
        double Npv(double rebate) => VannaVolga(BarrierType.UpOut, 1.40, 1.25, rebate);

        // QuantLib: 0.007748770547317221, the rebate-free price, at −0.01 (QuantLib-Python 1.44)
        Assert.Equal(0.007748770547317221, Npv(0.0), 1e-15);
        Assert.Equal(0.013413191989335214, Npv(0.01), 1e-15);
        Assert.Equal((2.0 * Npv(0.0)) - Npv(0.01), Npv(-0.01), 1e-15);
    }

    [Fact]
    public void AnImpliedVolatilityWithANegativeRebateRepricesItsTarget()
    {
        var today = new Date(15, October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        YieldTermStructure qTS = new FlatForward(today, 0.02, dc), rTS = new FlatForward(today, 0.05, dc);
        var process = new BlackScholesMertonProcess(
            100.0, qTS, rTS, new BlackConstantVol(today, new NullCalendar(), 0.25, dc));
        var option = new BarrierOption(
            BarrierType.DownOut, 90.0, -2.0, new PlainVanillaPayoff(
                OptionType.Call, 100.0), new EuropeanExercise(today + 365));

        // QuantLib: 0.12012082135179647, the volatility at which its engine, dropping the rebate, gives 6.0
        double impliedVol = option.ImpliedVolatility(6.0, process, context, 1e-8);
        Assert.True(Math.Abs(impliedVol - 0.12012082135179647) > 1e-2);

        var solved = new BlackScholesMertonProcess(
            100.0, qTS, rTS, new BlackConstantVol(today, new NullCalendar(), impliedVol, dc));
        Assert.Equal(6.0, AnalyticBarrierEngine.Calculate(option, context, solved).NPV!.Value, 1e-6);
    }

    // The Vanna-Volga engine on a smile flat at 12%: spot-delta quotes at a year, the ATM one delta-neutral.
    private static double VannaVolga(BarrierType type, double barrier, double strike, double rebate)
    {
        var today = new Date(15, June, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        return VannaVolgaBarrierEngine.Calculate(
            new BarrierOption(
                type, barrier, rebate, new PlainVanillaPayoff(
                    OptionType.Call, strike), new EuropeanExercise(today + 365)),
            context,
            new DeltaVolQuote(0.12, DeltaVolQuoteDeltaType.Spot, 1.0, DeltaVolQuoteAtmType.AtmDeltaNeutral),
            new DeltaVolQuote(-0.25, 0.12, 1.0, DeltaVolQuoteDeltaType.Spot),
            new DeltaVolQuote(0.25, 0.12, 1.0, DeltaVolQuoteDeltaType.Spot),
            1.30,
            new FlatForward(today, 0.03 * 365.0 / 360.0, dc),
            new FlatForward(today, 0.01 * 365.0 / 360.0, dc)).NPV!.Value;
    }
}
