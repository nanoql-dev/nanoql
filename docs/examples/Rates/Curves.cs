// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Examples.Rates;

/// <summary>Curves: a Euribor 6M curve bootstrapped from deposits and swaps, read back as discounts and rates.</summary>
public static class Curves
{
    /// <summary>Gets the example's evaluation date.</summary>
    public static Date Today { get; } = new(15, Month.October, 2026);

    /// <summary>Gets the example's context, the evaluation date alone.</summary>
    public static EvaluationContext Context { get; } = new() { EvaluationDate = Today };

    /// <summary>Bootstraps the curve on illustrative quotes.</summary>
    /// <param name="shift">A parallel shift added to every quote.</param>
    /// <returns>The curve.</returns>
    public static YieldTermStructure Build(double shift = 0.0)
    {
        Date today = Today;
        EvaluationContext context = Context;

        // <curve>
        (int Years, double Rate)[] swapQuotes =
            [(2, 0.0225), (3, 0.0232), (5, 0.0245), (7, 0.0256), (10, 0.0270), (15, 0.0283), (20, 0.0288), (30, 0.0285)];
        RateHelper[] helpers =
        [
            new DepositRateHelper(0.0205 + shift, new Euribor(new Period(3, TimeUnit.Months)), context),
            new DepositRateHelper(0.0210 + shift, new Euribor6M(), context),
            .. swapQuotes.Select(q =>
                new SwapRateHelper(q.Rate + shift, new EuriborSwapIsdaFixA(new Period(q.Years, TimeUnit.Years)), context)),
        ];
        var curve = new PiecewiseLogLinearDiscount(today, helpers, new Actual365Fixed(), context);
        // </curve>
        return curve;
    }

    /// <summary>The example's outputs.</summary>
    /// <returns>The curve's discounts, zero rates and 6-month forwards at a few tenors.</returns>
    public static IEnumerable<(string Name, Table Table)> Outputs()
    {
        YieldTermStructure curve = Build();
        var table = new Table("Tenor", "Date", ">Discount", ">Zero rate", ">6M forward");

        // <read>
        DayCounter dc = new Actual365Fixed();
        foreach (int years in new[] { 1, 2, 5, 10, 20, 25 })
        {
            Date d = Today + new Period(years, TimeUnit.Years);
            double discount = curve.Discount(d);
            double zero = curve.ZeroRate(d, dc, Compounding.Continuous).Rate;
            double forward = curve.ForwardRate(d, d + new Period(6, TimeUnit.Months), new Actual360(), Compounding.Simple).Rate;
            table.Add($"{years}Y", Table.D(d), Table.N(discount, 6), Table.Percent(zero), Table.Percent(forward));
        }

        // </read>

        yield return ("curves-read", table);
    }
}
