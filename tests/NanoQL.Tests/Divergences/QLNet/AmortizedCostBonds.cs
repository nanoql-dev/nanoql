// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Extensions.Instruments;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>The bonds the amortised-cost divergence facts build: QLNet's AB1 and AB2 (<c>testAmortizingBond1</c>,
/// <c>testAmortizingBond2</c>) and their variants, on <see cref="NullCalendar"/>.</summary>
internal static class AmortizedCostBonds
{
    /// <summary>The context the bonds are built in; their solves settle on their trade dates.</summary>
    public static readonly EvaluationContext Context = new() { EvaluationDate = new Date(16, September, 2015) };

    /// <summary>A bond held at amortised cost.</summary>
    /// <param name="face">The face amount.</param>
    /// <param name="cost">The cost.</param>
    /// <param name="coupon">The coupon rate.</param>
    /// <param name="issue">The issue date.</param>
    /// <param name="maturity">The maturity date.</param>
    /// <param name="trade">The trade date.</param>
    /// <param name="frequency">The coupon frequency.</param>
    /// <param name="dayCounter">The day counter, 30/360 US when <see langword="null"/>.</param>
    /// <param name="yield">A given effective rate, or <see langword="null"/> to solve it.</param>
    /// <param name="context">The context, <see cref="Context"/> when <see langword="null"/>.</param>
    /// <returns>The bond.</returns>
    public static AmortizingBond Bond(
        double face,
        double cost,
        double coupon,
        Date issue,
        Date maturity,
        Date trade,
        Frequency frequency = Frequency.Semiannual,
        DayCounter? dayCounter = null,
        double? yield = null,
        EvaluationContext? context = null) =>
        new(face, cost, coupon, issue, maturity, trade, frequency, dayCounter ?? new Thirty360(Thirty360Convention.USA),
            AmortizingMethod.EffectiveInterestRate, new NullCalendar(), context ?? Context, yield);

    /// <summary>QLNet's AB1: a premium bought on a coupon date.</summary>
    /// <param name="yield">A given effective rate, or <see langword="null"/>.</param>
    /// <returns>The bond.</returns>
    public static AmortizingBond Ab1(double? yield = null) =>
        Bond(40000, 43412, 0.06, new Date(1, January, 2001), new Date(1, January, 2005), new Date(1, January, 2004),
            yield: yield);

    /// <summary>QLNet's AB2: a discount bought between coupon dates.</summary>
    /// <returns>The bond.</returns>
    public static AmortizingBond Ab2() =>
        Bond(500000, 471444, 0.052, new Date(15, March, 1999), new Date(15, May, 2028), new Date(31, December, 2007));
}
