// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Examples.Rates;

/// <summary>Curves: a helper of one's own, <see cref="NcdRateHelper"/>, bootstrapped into a curve.</summary>
public static class CustomRateHelpers
{
    /// <summary>Gets the example's evaluation date, the curves page's.</summary>
    public static Date Today => Curves.Today;

    /// <summary>Gets the example's context, the evaluation date alone.</summary>
    public static EvaluationContext Context => Curves.Context;

    /// <summary>Gets the NCDs' tenors and rates: illustrative quotes.</summary>
    public static IReadOnlyList<(Period Tenor, double Rate)> Quotes { get; } =
    [
        (new Period(3, TimeUnit.Months), 0.0735), (new Period(6, TimeUnit.Months), 0.0750),
        (new Period(9, TimeUnit.Months), 0.0760), (new Period(1, TimeUnit.Years), 0.0770),
        (new Period(2, TimeUnit.Years), 0.0785), (new Period(3, TimeUnit.Years), 0.0800),
    ];

    /// <summary>Gets the NCDs' calendar.</summary>
    public static Calendar Calendar { get; } = new SouthAfrica();

    /// <summary>Gets the NCDs' and the curve's day counter.</summary>
    public static DayCounter DayCounter { get; } = new Actual365Fixed();

    /// <summary>Bootstraps the curve on the NCDs, quarterly.</summary>
    /// <param name="context">The context the helpers and the curve are dated in; the example's by default.</param>
    /// <returns>The curve.</returns>
    public static YieldTermStructure Build(EvaluationContext? context = null)
    {
        context ??= Context;

        // <curve>
        RateHelper[] helpers =
        [
            .. Quotes.Select(q =>
                new NcdRateHelper(q.Rate, q.Tenor, Frequency.Quarterly, Calendar, DayCounter, context)),
        ];
        var curve = new PiecewiseFlatForward(context.EvaluationDate, helpers, DayCounter, context);
        // </curve>
        return curve;
    }

    /// <summary>The bond an NCD pays as, from the example's evaluation date.</summary>
    /// <param name="tenor">The NCD's tenor.</param>
    /// <param name="rate">The NCD's rate.</param>
    /// <returns>The bond.</returns>
    public static FixedRateBond Bond(Period tenor, double rate) =>
        NcdRateHelper.MakeBond(Today, tenor, rate, Frequency.Quarterly, Calendar, DayCounter);

    /// <summary>The example's outputs.</summary>
    /// <returns>Each NCD's maturity, its discount on the curve, and its bond's dirty price there.</returns>
    public static IEnumerable<(string Name, Table Table)> Outputs()
    {
        YieldTermStructure curve = Build();
        var table = new Table("NCD", "Maturity", ">Rate", ">Discount", ">Dirty price");
        foreach (var (tenor, rate) in Quotes)
        {
            FixedRateBond bond = Bond(tenor, rate);
            double dirty = bond.DirtyPrice(DiscountingBondEngine.Calculate(bond, Context, curve), Context);
            Date maturity = bond.MaturityDate();
            table.Add(
                tenor.ToString(), Table.D(maturity), Table.Percent(rate, 2), Table.N(curve.Discount(maturity), 6),
                Table.N(dirty, 6));
        }

        yield return ("curves-custom-helper", table);
    }
}
