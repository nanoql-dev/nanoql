// SPDX-FileCopyrightText: Copyright (C) 2021 Ralf Konrad Eckel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/zeroinflationcashflow.hpp ql/cashflows/zeroinflationcashflow.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>
/// A cash flow paying a zero-inflation index ratio between the start and end dates less the observation lag: with the
/// dates in June and a three-month lag, the ratio is taken between March fixings. It isn't a coupon: it has no
/// accruals.
/// </summary>
public sealed class ZeroInflationCashFlow : IndexedCashFlow
{
    private readonly Date startDate;
    private readonly Date endDate;

    /// <summary>Initializes a new instance of the <see cref="ZeroInflationCashFlow"/> class.</summary>
    /// <param name="notional">The notional.</param>
    /// <param name="index">The zero-inflation index.</param>
    /// <param name="observationInterpolation">How the index is observed.</param>
    /// <param name="startDate">The start date; the base fixing is observed on it, with the lag.</param>
    /// <param name="endDate">The end date; the fixing is observed on it, with the lag.</param>
    /// <param name="observationLag">The observation lag.</param>
    /// <param name="paymentDate">The payment date.</param>
    /// <param name="growthOnly">Whether only the growth, the ratio less one, is paid.</param>
    /// <exception cref="ArgumentNullException"><paramref name="index"/> is null.</exception>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="observationInterpolation"/> isn't a member of its
    /// type.</exception>
    public ZeroInflationCashFlow(
        double notional,
        ZeroInflationIndex index,
        CPIInterpolationType observationInterpolation,
        Date startDate,
        Date endDate,
        Period observationLag,
        Date paymentDate,
        bool growthOnly = false)
        : base(
            notional,
            index,
            Lagged(startDate, observationLag, nameof(startDate)),
            Lagged(endDate, observationLag, nameof(endDate)),
            paymentDate,
            growthOnly)
    {
        EnumArguments.Check(observationInterpolation, nameof(observationInterpolation));
        ZeroInflationIndex = index;
        ObservationInterpolation = observationInterpolation;
        this.startDate = startDate;
        this.endDate = endDate;
        ObservationLag = observationLag;
    }

    /// <summary>Gets the zero-inflation index.</summary>
    public ZeroInflationIndex ZeroInflationIndex { get; }

    /// <summary>Gets how the index is observed.</summary>
    public CPIInterpolationType ObservationInterpolation { get; }

    /// <summary>Gets the observation lag.</summary>
    public Period ObservationLag { get; }

    /// <summary>The base fixing: the index observed on the start date with the lag.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The fixing.</returns>
    public override double BaseFixing(EvaluationContext context) =>
        CPI.LaggedFixing(ZeroInflationIndex, startDate, ObservationLag, ObservationInterpolation, context);

    /// <summary>The fixing: the index observed on the end date with the lag.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The fixing.</returns>
    public override double IndexFixing(EvaluationContext context) =>
        CPI.LaggedFixing(ZeroInflationIndex, endDate, ObservationLag, ObservationInterpolation, context);

    private static Date Lagged(Date d, Period lag, string paramName)
    {
        Date.ThrowIfDefault(d, paramName);
        return d - lag;
    }
}
