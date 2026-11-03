// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using NanoQL.Examples.Rates;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Examples.Stateless;

/// <summary>The stateless page's patterns: rebuild on change, memoize by inputs, price scenarios in parallel.</summary>
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
        // </parallel>

        var rebuild = new Table(">Quotes", ">NPV").Add("as quoted", Table.N(before)).Add("+1 bp", Table.N(after));
        yield return ("stateless-rebuild", rebuild);

        var scenarios = new Table(">Shift", ">NPV", ">Change");
        double base_ = npvs[Array.IndexOf(shifts, 0.0)];
        for (int i = 0; i < shifts.Length; i++)
        {
            scenarios.Add(
                (shifts[i] * 1e4).ToString("+0;-0;0", System.Globalization.CultureInfo.InvariantCulture) + " bp",
                Table.N(npvs[i]),
                Table.N(npvs[i] - base_));
        }

        yield return ("stateless-scenarios", scenarios);
    }
}
