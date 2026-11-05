// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2018 Jose Garcia
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: Examples/MulticurveBootstrapping/MulticurveBootstrapping.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Examples.Rates;

/// <summary>
/// Multi-curve bootstrapping: QuantLib's MulticurveBootstrapping example, an €STR curve from overnight indexed swaps,
/// then a Euribor 6M curve from a deposit, FRAs and swaps discounted on it, and two swaps priced on the two curves.
/// The example's Eonia quotes are taken as €STR's, whose conventions are Eonia's.
/// </summary>
public static class MulticurveBootstrapping
{
    /// <summary>Gets the example's evaluation context: 11 December 2012.</summary>
    public static EvaluationContext Context { get; } = new() { EvaluationDate = new Date(11, Month.December, 2012) };

    /// <summary>Gets the example's quote for the five-year swap.</summary>
    public static double FiveYearQuote => 0.00762;

    /// <summary>A swap's value on the two curves.</summary>
    /// <param name="Label">The swap.</param>
    /// <param name="Npv">Its NPV.</param>
    /// <param name="FairSpread">The spread over Euribor 6M at which it is worth nothing.</param>
    /// <param name="FairRate">The fixed rate at which it is worth nothing.</param>
    public sealed record SwapValue(string Label, double Npv, double FairSpread, double FairRate);

    /// <summary>The example's outputs.</summary>
    /// <returns>The two swaps at the market's five-year quote and at 0.90%.</returns>
    public static IEnumerable<(string Name, Table Table)> Outputs()
    {
        var swaps = new Table("5Y swap quote", "Swap", ">NPV", ">Fair spread", ">Fair fixed rate");
        foreach (double quote in new[] { FiveYearQuote, 0.0090 })
        {
            foreach (SwapValue s in Price(quote))
            {
                swaps.Add(Table.Percent(quote, 2), s.Label, Table.N(s.Npv, 2), Table.Percent(s.FairSpread, 2),
                    Table.Percent(s.FairRate, 2));
            }
        }

        yield return ("multi-curve-swaps", swaps);
    }

    /// <summary>The €STR curve, from deposits and overnight indexed swaps.</summary>
    /// <returns>The curve, extrapolating past its last quote.</returns>
    public static YieldTermStructure EstrCurve()
    {
        EvaluationContext context = Context;
        // <estr>
        Calendar calendar = new TARGET();
        Date today = context.EvaluationDate;
        var estr = new Estr();
        var helpers = new List<BootstrapHelper<YieldTermStructure>>();

        // Overnight, tomorrow-next and spot-next deposits.
        foreach (int fixingDays in new[] { 0, 1, 2 })
        {
            helpers.Add(new DepositRateHelper(0.0004, new Period(1, TimeUnit.Days), fixingDays, calendar,
                BusinessDayConvention.Following, false, new Actual360(), context));
        }

        // Short OIS, then OIS between ECB meeting dates, then long OIS.
        foreach (var (n, unit, rate) in new[]
                 {
                     (1, TimeUnit.Weeks, 0.00070), (2, TimeUnit.Weeks, 0.00069), (3, TimeUnit.Weeks, 0.00078),
                     (1, TimeUnit.Months, 0.00074),
                 })
        {
            helpers.Add(new OISRateHelper(2, new Period(n, unit), rate, estr, context));
        }

        foreach (var (start, end, rate) in new[]
                 {
                     (new Date(16, Month.January, 2013), new Date(13, Month.February, 2013), 0.000460),
                     (new Date(13, Month.February, 2013), new Date(13, Month.March, 2013), 0.000160),
                     (new Date(13, Month.March, 2013), new Date(10, Month.April, 2013), -0.000070),
                     (new Date(10, Month.April, 2013), new Date(8, Month.May, 2013), -0.000130),
                     (new Date(8, Month.May, 2013), new Date(12, Month.June, 2013), -0.000140),
                 })
        {
            helpers.Add(new OISRateHelper(start, end, rate, estr, context));
        }

        foreach (var (n, unit, rate) in new[]
                 {
                     (15, TimeUnit.Months, 0.00002), (18, TimeUnit.Months, 0.00008), (21, TimeUnit.Months, 0.00021),
                     (2, TimeUnit.Years, 0.00036), (3, TimeUnit.Years, 0.00127), (4, TimeUnit.Years, 0.00274),
                     (5, TimeUnit.Years, 0.00456), (6, TimeUnit.Years, 0.00647), (7, TimeUnit.Years, 0.00827),
                     (8, TimeUnit.Years, 0.00996), (9, TimeUnit.Years, 0.01147), (10, TimeUnit.Years, 0.0128),
                     (11, TimeUnit.Years, 0.01404), (12, TimeUnit.Years, 0.01516), (15, TimeUnit.Years, 0.01764),
                     (20, TimeUnit.Years, 0.01939), (25, TimeUnit.Years, 0.02003), (30, TimeUnit.Years, 0.02038),
                 })
        {
            helpers.Add(new OISRateHelper(2, new Period(n, unit), rate, estr, context));
        }

        // QuantLib's MonotonicLogCubic: a cubic spline on the discounts' logarithms, Hyman's filter keeping it monotonic.
        YieldTermStructure estrCurve = new PiecewiseLogCubicDiscount(today, helpers, new Actual365Fixed(), context,
            i: new LogCubic(CubicInterpolationDerivativeApprox.Spline, monotonic: true)).EnableExtrapolation();
        // </estr>
        return estrCurve;
    }

    /// <summary>The Euribor 6M curve, from a deposit, FRAs and swaps discounted on €STR.</summary>
    /// <param name="estrCurve">The €STR curve the swaps are discounted on.</param>
    /// <param name="fiveYearQuote">The five-year swap's quote.</param>
    /// <returns>The curve.</returns>
    public static YieldTermStructure Euribor6MCurve(YieldTermStructure estrCurve, double fiveYearQuote)
    {
        EvaluationContext context = Context;
        // <euribor>
        Calendar calendar = new TARGET();
        Date settlement = calendar.Advance(context.EvaluationDate, 2, TimeUnit.Days);
        var euribor6M = new Euribor6M();
        var helpers = new List<BootstrapHelper<YieldTermStructure>>
        {
            new DepositRateHelper(0.00312, new Period(6, TimeUnit.Months), 3, calendar, BusinessDayConvention.Following,
                false, new Actual360(), context),
        };

        // FRAs starting in 1 to 18 months, each on Euribor 6M.
        double[] fras =
        [
            0.002930, 0.002720, 0.002600, 0.002560, 0.002520, 0.002480, 0.002540, 0.002610, 0.002670, 0.002790, 0.002910,
            0.003030, 0.003180, 0.003350, 0.003520, 0.003710, 0.003890, 0.004090,
        ];
        for (int months = 1; months <= fras.Length; ++months)
        {
            helpers.Add(new FraRateHelper(fras[months - 1], months, euribor6M, context));
        }

        // Swaps against Euribor 6M, annual 30/360 fixed legs, discounted on the €STR curve.
        foreach (var (years, rate) in new[]
                 {
                     (3, 0.004240), (4, 0.005760), (5, fiveYearQuote), (6, 0.009540), (7, 0.011350), (8, 0.013030),
                     (9, 0.014520), (10, 0.015840), (12, 0.018090), (15, 0.020370), (20, 0.021870), (25, 0.022340),
                     (30, 0.022560), (35, 0.022950), (40, 0.023480), (50, 0.024210), (60, 0.024630),
                 })
        {
            helpers.Add(new SwapRateHelper(rate, new Period(years, TimeUnit.Years), calendar, Frequency.Annual,
                BusinessDayConvention.Unadjusted, new Thirty360(Thirty360Convention.European), euribor6M, context,
                discountingCurve: estrCurve));
        }

        YieldTermStructure euriborCurve = new PiecewiseLogCubicDiscount(settlement, helpers, new Actual365Fixed(), context,
            i: new LogCubic(CubicInterpolationDerivativeApprox.Spline, monotonic: true),
            bootstrap: new IterativeBootstrap(1.0e-15));
        // </euribor>
        return euriborCurve;
    }

    /// <summary>Prices the example's two payer swaps at 0.70%, the spot five-year and the five-year a year forward.</summary>
    /// <param name="fiveYearQuote">The five-year swap's quote the Euribor curve is built on.</param>
    /// <returns>The two swaps' values.</returns>
    public static IReadOnlyList<SwapValue> Price(double fiveYearQuote)
    {
        EvaluationContext context = Context;
        YieldTermStructure estrCurve = EstrCurve();
        // <swaps>
        YieldTermStructure euriborCurve = Euribor6MCurve(estrCurve, fiveYearQuote);
        Calendar calendar = new TARGET();
        Date settlement = calendar.Advance(context.EvaluationDate, 2, TimeUnit.Days);

        SwapValue Value(string label, Date start)
        {
            Date maturity = start + new Period(5, TimeUnit.Years);
            var fixedSchedule = new Schedule(start, maturity, new Period(Frequency.Annual), calendar,
                BusinessDayConvention.Unadjusted, BusinessDayConvention.Unadjusted, DateGenerationRule.Forward, false,
                context);
            var floatSchedule = new Schedule(start, maturity, new Period(Frequency.Semiannual), calendar,
                BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward,
                false, context);
            var swap = new VanillaSwap(SwapType.Payer, 1_000_000.0, fixedSchedule, 0.007,
                new Thirty360(Thirty360Convention.European), floatSchedule, new Euribor6M(euriborCurve), 0.0,
                new Actual360(), context);

            // Forecast on the Euribor curve, discounted on €STR.
            var result = DiscountingSwapEngine.Calculate(swap, context, estrCurve);
            return new SwapValue(label, result.NPV!.Value, result.FairSpread!.Value, result.FairRate!.Value);
        }

        SwapValue[] values =
        [
            Value("5-year swap paying 0.70%", settlement),
            Value("5-year swap a year forward, paying 0.70%", calendar.Advance(settlement, 1, TimeUnit.Years)),
        ];
        // </swaps>
        return values;
    }
}
