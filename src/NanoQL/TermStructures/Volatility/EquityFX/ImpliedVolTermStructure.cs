// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/equityfx/impliedvoltermstructure.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The Black-volatility structure another implies at a date in the future, its reference date: the original's forward
/// variance from that date.
/// </summary>
/// <remarks>
/// <para>
/// Immutable over its original (the S-van plan's VD24): QuantLib's structure follows its handle, and an original that
/// changes is a new structure here. Its day counter, maximum date and strike range are the original's. As QuantLib warns,
/// an implied structure makes financial sense only over an original that depends on time alone, not on the asset.
/// </para>
/// <para>
/// A time on the structure is read from the original between the reference date's time on the original's clock and
/// that time plus the time, QuantLib's sum, which assumes year fractions add. A date is read by date where they don't:
/// where the sum and the date's own time on the original's clock differ by more than 1e-12 relative, the structure's
/// variance at a date is the original's forward variance from the reference date to that date by date, and its
/// volatilities and forward variances by date follow it. On Thirty360 QuantLib's sum misses that variance, by +3.70% at
/// 28 February and +0.75% at 30 June over a constant volatility (the S-van plan's VR4 (xxii)); on every other date, and
/// on the day counters whose year fractions add, the structure keeps QuantLib's bits. A reference date before the
/// original's accrues no variance before the original's, where QuantLib's read counts the original's variance at a
/// negative time (the mismatched-dates class). A documented divergence.
/// </para>
/// </remarks>
public sealed class ImpliedVolTermStructure : BlackVarianceTermStructure
{
    // the relative difference beyond which QuantLib's sum of times doesn't denote a date's time
    private const double SumTolerance = 1.0e-12;

    private readonly BlackVolTermStructure originalTS;
    private readonly double timeShift;

    /// <summary>Initializes a new instance of the <see cref="ImpliedVolTermStructure"/> class.</summary>
    /// <param name="originalTS">The structure the implied structure is read from.</param>
    /// <param name="referenceDate">The implied reference date.</param>
    /// <exception cref="ArgumentNullException"><paramref name="originalTS"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="referenceDate"/> is <c>default(Date)</c>.</exception>
    public ImpliedVolTermStructure(BlackVolTermStructure originalTS, Date referenceDate)
        : base(referenceDate, null, BusinessDayConvention.Following, Original(originalTS).DayCounter)
    {
        this.originalTS = originalTS;

        // the original is immutable, so the shift is read once, where QuantLib reads it at every query
        timeShift = DayCounter.YearFraction(originalTS.ReferenceDate, ReferenceDate);
    }

    /// <summary>Gets the original's maximum date.</summary>
    public override Date MaxDate => originalTS.MaxDate;

    /// <summary>The original's minimum strike.</summary>
    /// <returns>The minimum strike.</returns>
    public override double MinStrike() => originalTS.MinStrike();

    /// <summary>The original's maximum strike.</summary>
    /// <returns>The maximum strike.</returns>
    public override double MaxStrike() => originalTS.MaxStrike();

    // A date's variance: QuantLib's sum where it denotes the date's time on the original's clock, and the original's
    // forward variance by date where it doesn't.
    internal override double BlackVarianceOn(Date d, double t, double strike) =>
        SumHolds(d, t) ? BlackVarianceImpl(t, strike) : VarianceByDate(d, strike);

    // A date's volatility, from its variance by date where QuantLib's sum doesn't hold.
    internal override double BlackVolOn(Date d, double t, double strike) =>
        SumHolds(d, t) ? BlackVolImpl(t, strike) : Math.Sqrt(VarianceByDate(d, strike) / t);

    /// <summary>
    /// The original's forward variance from the reference date's time on its clock to that time plus the time, none
    /// before the original's reference date.
    /// </summary>
    /// <param name="t">The time.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The variance.</returns>
    protected override double BlackVarianceImpl(double t, double strike)
    {
        // t is relative to the current reference date and needs to be converted to the time relative to the reference
        // date of the original curve; nothing accrues before the original's reference date
        double time1 = timeShift, time2 = timeShift + t;
        return originalTS.BlackForwardVariance(
            time1 < 0.0 ? 0.0 : time1, time2 < 0.0 ? 0.0 : time2, strike, true);
    }

    private bool SumHolds(Date d, double t)
    {
        double summed = timeShift + t;
        double own = originalTS.TimeFromReference(d);
        return Math.Abs(summed - own) <= SumTolerance * Math.Abs(own);
    }

    // the original's forward variance from the reference date to a date, by date, none before its own reference date
    private double VarianceByDate(Date d, double strike)
    {
        Date origin = originalTS.ReferenceDate;
        return originalTS.BlackForwardVariance(
            ReferenceDate < origin ? origin : ReferenceDate, d < origin ? origin : d, strike, true);
    }

    // The original, refused when null before the base constructor reads it.
    private static BlackVolTermStructure Original(BlackVolTermStructure originalTS)
    {
        ArgumentNullException.ThrowIfNull(originalTS);
        return originalTS;
    }
}
