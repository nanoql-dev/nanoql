// SPDX-FileCopyrightText: Copyright (C) 2011 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2022 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/cpicoupon.hpp ql/cashflows/cpicoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>
/// A cash flow paying the performance of a zero-inflation (CPI) index: the notional times the ratio of the index
/// observed on the observation date, with the lag, to its base. It isn't a coupon: it has no accruals.
/// </summary>
/// <remarks>
/// The base is the given base fixing, which needn't agree with the index, or the index observed on the base date with
/// no lag. A <see cref="CPILeg"/>'s notional flow takes its coupons' base instead, as <see cref="CPICoupon"/>'s remarks
/// say; the public constructor keeps QuantLib's convention.
/// </remarks>
public sealed class CPICashFlow : IndexedCashFlow
{
    // The observation whose lagged fixing is the base, when no base fixing is given: QuantLib's baseDate at a zero lag
    // from the public constructor; the leg's own from CPILeg.
    private readonly (Date Date, Period Lag)? baseObservation;

    private readonly double? baseFixing;

    /// <summary>Initializes a new instance of the <see cref="CPICashFlow"/> class.</summary>
    /// <param name="notional">The notional.</param>
    /// <param name="index">The zero-inflation index.</param>
    /// <param name="baseDate">The base date, or <see langword="null"/>, QuantLib's <c>Date()</c>.</param>
    /// <param name="baseFixing">The base fixing, or <see langword="null"/>, QuantLib's <c>Null&lt;Real&gt;()</c>; used
    /// when given.</param>
    /// <param name="observationDate">The observation date, before the lag.</param>
    /// <param name="observationLag">The observation lag.</param>
    /// <param name="interpolation">How the index is observed.</param>
    /// <param name="paymentDate">The payment date.</param>
    /// <param name="growthOnly">Whether only the growth, the ratio less one, is paid.</param>
    /// <exception cref="ArgumentNullException"><paramref name="index"/> is null, QuantLib's "no index
    /// provided".</exception>
    /// <exception cref="ArgumentException">Neither a base fixing nor a base date is given, the base fixing is below
    /// 1e-16 in absolute value, or a date is <c>default(Date)</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="interpolation"/> isn't a member of its
    /// type.</exception>
    public CPICashFlow(
        double notional,
        ZeroInflationIndex index,
        Date? baseDate,
        double? baseFixing,
        Date observationDate,
        Period observationLag,
        CPIInterpolationType interpolation,
        Date paymentDate,
        bool growthOnly = false)
        : this(
            notional,
            index,
            baseDate,
            baseFixing,
            baseDate is Date d ? (d, new Period(0, TimeUnit.Months)) : null,
            observationDate,
            observationLag,
            interpolation,
            paymentDate,
            growthOnly)
    {
    }

    // The leg's constructor: the base observation given, one base for every flow of a leg.
    internal CPICashFlow(
        double notional,
        ZeroInflationIndex index,
        Date? baseDate,
        double? baseFixing,
        (Date Date, Period Lag)? baseObservation,
        Date observationDate,
        Period observationLag,
        CPIInterpolationType interpolation,
        Date paymentDate,
        bool growthOnly)
        : base(notional, index, baseDate, FixingDateOf(observationDate, observationLag), paymentDate, growthOnly)
    {
        EnumArguments.Check(interpolation, nameof(interpolation));
        if (baseFixing is null && baseDate is null)
        {
            throw new ArgumentException(
                "baseCPI and baseDate can not be both null, provide a valid baseCPI or baseDate", nameof(baseFixing));
        }

        if (baseFixing is double f && !(Math.Abs(f) > 1e-16))
        {
            throw new ArgumentException("|baseCPI_| < 1e-16, future divide-by-zero problem", nameof(baseFixing));
        }

        this.baseFixing = baseFixing;
        this.baseObservation = baseObservation;
        ObservationDate = observationDate;
        ObservationLag = observationLag;
        Interpolation = interpolation;
        CpiIndex = index;
        Frequency = index.Frequency;
    }

    /// <summary>Gets the observation date, before the lag.</summary>
    public Date ObservationDate { get; }

    /// <summary>Gets the observation lag.</summary>
    public Period ObservationLag { get; }

    /// <summary>Gets how the index is observed: flat, linear, or as the index.</summary>
    public CPIInterpolationType Interpolation { get; }

    /// <summary>Gets the index's frequency.</summary>
    public Frequency Frequency { get; }

    /// <summary>Gets the zero-inflation index.</summary>
    public ZeroInflationIndex CpiIndex { get; }

    /// <summary>The base date.</summary>
    /// <returns>The base date.</returns>
    /// <exception cref="NanoQLException">No base date was given.</exception>
    public override Date? BaseDate() => base.BaseDate() ?? throw new NanoQLException("no base date specified");

    /// <summary>The base fixing: the given one, which needn't agree with the index, or the index observed on the base
    /// date.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The base fixing.</returns>
    public override double BaseFixing(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (baseFixing is double f)
        {
            return f;
        }

        (Date date, Period lag) = baseObservation!.Value;
        return CPI.LaggedFixing(CpiIndex, date, lag, Interpolation, context);
    }

    /// <summary>The fixing: the index observed on the observation date with the lag.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The fixing.</returns>
    public override double IndexFixing(EvaluationContext context) =>
        CPI.LaggedFixing(CpiIndex, ObservationDate, ObservationLag, Interpolation, context);

    private static Date FixingDateOf(Date observationDate, Period observationLag)
    {
        Date.ThrowIfDefault(observationDate, nameof(observationDate));
        return observationDate - observationLag;
    }
}
