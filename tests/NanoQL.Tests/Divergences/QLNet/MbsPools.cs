// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Extensions.Instruments;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>The pools the MBS divergence facts build: QLNet's Fabozzi pool (<c>testMBSFixedBondCached</c>) and its
/// variants, 400 million from 1 February 2007, 30/360 Bond Basis.</summary>
internal static class MbsPools
{
    /// <summary>The pools' start.</summary>
    public static readonly Date Start = new(1, February, 2007);

    /// <summary>A monthly pool on <see cref="NullCalendar"/>, paying unadjusted, unless given otherwise.</summary>
    /// <param name="tenor">The remaining term, in months.</param>
    /// <param name="original">The original term, in months.</param>
    /// <param name="wac">The WAC.</param>
    /// <param name="passThrough">The pass-through rate.</param>
    /// <param name="model">The prepayment model.</param>
    /// <param name="frequency">The frequency.</param>
    /// <param name="calendar">The calendar, <see cref="NullCalendar"/> when <see langword="null"/>.</param>
    /// <param name="convention">The payment convention.</param>
    /// <returns>The pool.</returns>
    public static MBSFixedRateBond Pool(
        int tenor,
        int original,
        double wac,
        double passThrough,
        IPrepayModel? model,
        Frequency frequency = Frequency.Monthly,
        Calendar? calendar = null,
        BusinessDayConvention convention = BusinessDayConvention.Unadjusted) =>
        new(0, calendar ?? new NullCalendar(), 400000000, Start, new Period(tenor, TimeUnit.Months),
            new Period(original, TimeUnit.Months), frequency, wac, passThrough,
            new Thirty360(Thirty360Convention.BondBasis), model, convention);

    /// <summary>QLNet's Fabozzi pool: 358 months of 360, a WAC of 6% and a 5.5% pass-through at 100% PSA.</summary>
    /// <returns>The pool.</returns>
    public static MBSFixedRateBond Fabozzi() => Pool(358, 360, 0.06, 0.055, new PSACurve(Start));

    /// <summary>A context on a date.</summary>
    /// <param name="d">The evaluation date.</param>
    /// <returns>The context.</returns>
    public static EvaluationContext At(Date d) => new() { EvaluationDate = d };
}
