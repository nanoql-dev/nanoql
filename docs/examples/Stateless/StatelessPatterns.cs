// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using NanoQL.Coupons;
using NanoQL.Examples.Rates;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Examples.Stateless;

/// <summary>
/// The stateless page's patterns: rebuild on change, memoize by inputs, price scenarios in parallel, value as of a
/// past date.
/// </summary>
public static class StatelessPatterns
{
    /// <summary>The example's outputs.</summary>
    /// <returns>The swap's value under each parallel shift of the quotes.</returns>
    public static IEnumerable<(string Name, Table Table)> Outputs()
    {
        EvaluationContext context = Curves.Context;

        // <rebuild>
        // A swap is a value: its price is a function of the swap, the context and the curve.
        static double Price(YieldTermStructure curve, EvaluationContext context)
        {
            VanillaSwap swap = new MakeVanillaSwap(new Period(10, TimeUnit.Years), new Euribor6M(curve))
                .WithFixedRate(0.027)
                .WithNominal(10e6)
                .ToVanillaSwap(context);
            return DiscountingSwapEngine.Calculate(swap, context, curve).NPV!.Value;
        }

        // A quote moves: build the curve again from the new quotes, and price on it.
        double before = Price(Curves.Build(), context);
        double after = Price(Curves.Build(shift: 0.0001), context);
        // </rebuild>

        // <memoize>
        // The same inputs give the same curve, so a cache keyed by the inputs can share it, across threads too.
        var curves = new ConcurrentDictionary<double, YieldTermStructure>();
        YieldTermStructure CurveFor(double shift) => curves.GetOrAdd(shift, s => Curves.Build(s));
        // </memoize>

        // <parallel>
        double[] shifts = [-0.0050, -0.0025, -0.0010, 0.0, 0.0010, 0.0025, 0.0050];
        double[] npvs = new double[shifts.Length];
        Parallel.For(0, shifts.Length, i => npvs[i] = Price(CurveFor(shifts[i]), context));

        // The same scenarios priced one after another give the same bits.
        double[] inSequence = [.. shifts.Select(shift => Price(CurveFor(shift), context))];
        // </parallel>

        var rebuild = new Table(">Quotes", ">NPV").Add("as quoted", Table.N(before)).Add("+1 bp", Table.N(after));
        yield return ("stateless-rebuild", rebuild);

        var scenarios = new Table(">Shift", ">NPV", ">Change", ">In sequence");
        double base_ = npvs[Array.IndexOf(shifts, 0.0)];
        for (int i = 0; i < shifts.Length; i++)
        {
            scenarios.Add(
                (shifts[i] * 1e4).ToString("+0;-0;0", System.Globalization.CultureInfo.InvariantCulture) + " bp",
                Table.N(npvs[i]),
                Table.N(npvs[i] - base_),
                BitConverter.DoubleToInt64Bits(inSequence[i]) == BitConverter.DoubleToInt64Bits(npvs[i])
                    ? "the same bits"
                    : Table.N(inSequence[i]));
        }

        yield return ("stateless-scenarios", scenarios);

        var asOf = new Table("As of", ">Curve", ">Fixings", ">NPV");
        foreach (var (date, curveRate, fixings, npv) in ValueAsOfTwoDates())
        {
            asOf.Add(Table.D(date), curveRate, fixings.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Table.N(npv));
        }

        yield return ("stateless-as-of", asOf);
    }

    /// <summary>
    /// One swap valued as of its trade date and seven months on, each valuation on that day's curve and, seven months on,
    /// with the index's fixings since the trade.
    /// </summary>
    /// <param name="laterFirst">Whether the valuation seven months on runs before the trade date's.</param>
    /// <returns>Each valuation's date, its curve, how many fixings its context holds, and the NPV.</returns>
    public static IReadOnlyList<(Date Date, string Curve, int Fixings, double Npv)> ValueAsOfTwoDates(
        bool laterFirst = false)
    {
        EvaluationContext tradeContext = Curves.Context;
        // <asof>
        Date start = new TARGET().Advance(tradeContext.EvaluationDate, 2, TimeUnit.Days);
        Date later = new TARGET().Advance(tradeContext.EvaluationDate, 7, TimeUnit.Months);
        static VanillaSwap Trade(YieldTermStructure curve, EvaluationContext context, Date start) =>
            new MakeVanillaSwap(new Period(5, TimeUnit.Years), new Euribor6M(curve))
                .WithEffectiveDate(start)
                .WithFixedRate(0.027)
                .WithNominal(10e6)
                .ToVanillaSwap(context);

        // As of the trade date, on that day's curve.
        double AtTrade()
        {
            YieldTermStructure curve = Curves.Build();
            return DiscountingSwapEngine.Calculate(Trade(curve, tradeContext, start), tradeContext, curve).NPV!.Value;
        }

        // Seven months on: that day's curve, and the fixings the index published since the trade.
        (double Npv, int Fixings) SevenMonthsOn()
        {
            YieldTermStructure curve = new FlatForward(later, 0.0300, new Actual365Fixed());
            var euribor6M = new Euribor6M(curve);
            IndexFixings fixings = tradeContext.Fixings;
            double[] published = [0.0251, 0.0262];
            int k = 0;
            foreach (CashFlow flow in Trade(curve, tradeContext, start).FloatingLeg)
            {
                if (flow is IborCoupon coupon && coupon.FixingDate() < later)
                {
                    fixings = fixings.AddFixing(euribor6M, coupon.FixingDate(), published[k++]);
                }
            }

            EvaluationContext context = tradeContext with { EvaluationDate = later, Fixings = fixings };
            return (DiscountingSwapEngine.Calculate(Trade(curve, context, start), context, curve).NPV!.Value, k);
        }
        // </asof>

        (double Npv, int Fixings) sevenMonthsOn = laterFirst ? SevenMonthsOn() : default;
        double atTrade = AtTrade();
        if (!laterFirst)
        {
            sevenMonthsOn = SevenMonthsOn();
        }

        return
        [
            (tradeContext.EvaluationDate, "bootstrapped", 0, atTrade),
            (later, "flat 3.00%", sevenMonthsOn.Fixings, sevenMonthsOn.Npv),
        ];
    }
}
