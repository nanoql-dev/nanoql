// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/basecorrelationstructure.hpp ql/experimental/credit/basecorrelationstructure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: ImplicitCorrelation(Real, Real), never ported: basecorrelationstructure.hpp declares it and nothing defines it

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.Experimental;

/// <summary>
/// The matrix-based base correlation term structure: a loss level against time interpolated scalar copula-type
/// parametric correlation, the correlation of a given portfolio's credit loss at a loss level and time.
/// </summary>
/// <typeparam name="TInterpolator">The 2-D interpolator over the tranche times and loss levels.</typeparam>
/// <remarks>
/// <para>
/// Immutable: the quotes are values, read at construction (the S-xcr plan's XR5), and a structure whose reference date
/// follows the evaluation date is built by <see cref="Create"/> from the context (D15). QuantLib specializes the
/// interpolation for <c>BilinearInterpolation</c> and <c>BicubicSpline</c> alone; any 2-D interpolator builds it here,
/// <see cref="Bilinear"/> and <see cref="Bicubic"/> among them. Some interpolators may take the correlation out of
/// [−1, 1].
/// </para>
/// <para>
/// The correlations hold a row per loss level and a column per tenor, the shape QuantLib's checks enforce, where its
/// header names them <c>correls[iYear][iLoss]</c>. The loss levels are checked as QuantLib's <c>checkLosses</c> checks
/// them, increasing in (0, 1], a check QuantLib defines and never calls, so it interpolates on unsorted levels.
/// </para>
/// <para>
/// The correlation is read from the interpolation with extrapolation always on, bilinear off the grid where the
/// interpolator is, so off the grid it can leave [0, 1]; the <c>extrapolate</c> flag is QuantLib's and unread there too
/// (the S-xcr plan's XR8 (xl)). The base-correlation model refuses a correlation outside [0, 1) (XR8 (xxxii)).
/// </para>
/// <para>
/// To be done, as in QuantLib: the relation to a given basket is to be made explicit for bespoke models; a matrix
/// data structure, and the data structure for a basket whose current losses aren't zero. The 2-D interpolator is left
/// open because of the arbitrage issues of its choice: non-local ones like cubic splines affect the past (calibrated)
/// coupons of previous tenors.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class BaseCorrelationTermStructure<TInterpolator> : CorrelationTermStructure
    where TInterpolator : struct, IInterpolator2D
{
    private readonly ImmutableArray<Date> trancheDates;
    private readonly Interpolation2D interpolation;

    private BaseCorrelationTermStructure(
        Date referenceDate,
        int settlementDays,
        Calendar calendar,
        BusinessDayConvention bdc,
        ImmutableArray<Period> tenors,
        ImmutableArray<double> lossLevel,
        Matrix correlations,
        DayCounter dayCounter,
        TInterpolator interpolator)
        : base(referenceDate, calendar, bdc, dayCounter, settlementDays)
    {
        trancheDates = [.. tenors.Select(tenor => calendar.Advance(ReferenceDate, tenor, BusinessDayConvention))];
        double[] trancheTimes = [.. trancheDates.Select(TimeFromReference)];
        interpolation = interpolator.Interpolate(trancheTimes, lossLevel.AsSpan(), correlations);
    }

    /// <inheritdoc/>
    public override int CorrelationSize => 1;

    /// <inheritdoc/>
    public override Date MaxDate => trancheDates[^1];

    /// <summary>Builds the surface, its reference date the evaluation date advanced by the settlement days, which
    /// should in an ideal world coincide with the basket's (implicit) inception and its default curves' settlement
    /// dates.</summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="calendar">The calendar.</param>
    /// <param name="bdc">The convention adjusting the tranche dates.</param>
    /// <param name="tenors">The tranche tenors, positive and increasing.</param>
    /// <param name="lossLevel">The loss levels, fractions of the portfolio, increasing in (0, 1].</param>
    /// <param name="correls">The correlations, a row per loss level and a column per tenor.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="interpolator">The interpolator's settings.</param>
    /// <returns>The surface.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative, or
    /// <paramref name="bdc"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">There are no correlations or a row of another length, where QuantLib reads
    /// past the first row's end; the first tenor isn't positive ("first tranche tenor is negative") or the tenors don't
    /// increase ("non increasing tranche tenor"); a loss level isn't in (0, 1] or the levels don't increase; the
    /// matrix doesn't have a row per loss level and a column per tenor ("mismatch between number of …"); or the
    /// interpolator can't interpolate the grid.</exception>
    public static BaseCorrelationTermStructure<TInterpolator> Create(
        int settlementDays,
        Calendar calendar,
        BusinessDayConvention bdc,
        IReadOnlyList<Period> tenors,
        IReadOnlyList<double> lossLevel,
        IReadOnlyList<IReadOnlyList<double>> correls,
        DayCounter dayCounter,
        EvaluationContext context,
        TInterpolator interpolator = default)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(tenors);
        ArgumentNullException.ThrowIfNull(lossLevel);
        ArgumentNullException.ThrowIfNull(correls);
        ArgumentNullException.ThrowIfNull(dayCounter);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        EnumArguments.Check(bdc, nameof(bdc));
        if (correls.Count == 0 || correls[0] is null || correls[0].Count == 0)
        {
            throw new ArgumentException("no correlations given", nameof(correls));
        }

        int columns = correls[0].Count;
        var correlations = new Matrix(correls.Count, columns);
        for (int i = 0; i < correls.Count; i++)
        {
            IReadOnlyList<double> row = correls[i] ?? throw new ArgumentNullException(nameof(correls));
            if (row.Count != columns)
            {
                throw new ArgumentException(
                    $"correlation row {i} has {row.Count} entries where the first has {columns}", nameof(correls));
            }

            for (int j = 0; j < columns; j++)
            {
                correlations[i, j] = row[j];
            }
        }

        ImmutableArray<Period> tenorList = [.. tenors];
        ImmutableArray<double> losses = [.. lossLevel];
        CheckTrancheTenors(tenorList);
        CheckLosses(losses);
        CheckInputs(tenorList.Length, losses.Length, correlations.Rows, correlations.Columns);

        Date referenceDate = calendar.Advance(context.EvaluationDate, settlementDays, TimeUnit.Days);
        return new BaseCorrelationTermStructure<TInterpolator>(
            referenceDate, settlementDays, calendar, bdc, tenorList, losses, correlations, dayCounter, interpolator);
    }

    /// <summary>The base correlation at a date and loss level.</summary>
    /// <param name="d">The date.</param>
    /// <param name="lossLevel">The loss level, a fraction of the portfolio.</param>
    /// <param name="extrapolate">QuantLib's flag, unread: the interpolation always extrapolates.</param>
    /// <returns>The correlation.</returns>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    public double Correlation(Date d, double lossLevel, bool extrapolate = false) =>
        Correlation(TimeFromReference(d), lossLevel, extrapolate);

    /// <summary>The base correlation at a time and loss level.</summary>
    /// <param name="t">The time.</param>
    /// <param name="lossLevel">The loss level, a fraction of the portfolio.</param>
    /// <param name="extrapolate">QuantLib's flag, unread: the interpolation always extrapolates.</param>
    /// <returns>The correlation.</returns>
    public double Correlation(double t, double lossLevel, bool extrapolate = false)
    {
        _ = extrapolate;
        return interpolation.Evaluate(t, lossLevel, true);
    }

    private static void CheckTrancheTenors(ImmutableArray<Period> tenors)
    {
        // QuantLib reads the first tenor of an empty list
        if (tenors.IsEmpty)
        {
            throw new ArgumentException("no tranche tenors given", nameof(tenors));
        }

        if (!(tenors[0] > new Period(0, TimeUnit.Days)))
        {
            throw new ArgumentException($"first tranche tenor is negative ({tenors[0]})", nameof(tenors));
        }

        for (int i = 1; i < tenors.Length; ++i)
        {
            if (!(tenors[i] > tenors[i - 1]))
            {
                throw new ArgumentException(
                    $"non increasing tranche tenor: {DataFormattersFunctions.Ordinal(i)} is {tenors[i - 1]}, " +
                    $"{DataFormattersFunctions.Ordinal(i + 1)} is {tenors[i]}",
                    nameof(tenors));
            }
        }
    }

    private static void CheckLosses(ImmutableArray<double> lossLevel)
    {
        if (lossLevel.IsEmpty)
        {
            throw new ArgumentException("no loss levels given", nameof(lossLevel));
        }

        if (!(lossLevel[0] > 0.0))
        {
            throw new ArgumentException(
                string.Create(System.Globalization.CultureInfo.InvariantCulture, $"first loss level is negative ({lossLevel[0]})"), nameof(lossLevel));
        }

        if (!(lossLevel[0] <= 1.0))
        {
            throw new ArgumentException(
                string.Create(System.Globalization.CultureInfo.InvariantCulture, $"First loss level larger than 100% ({lossLevel[0]})"), nameof(lossLevel));
        }

        for (int i = 1; i < lossLevel.Length; ++i)
        {
            if (!(lossLevel[i] > lossLevel[i - 1]))
            {
                throw new ArgumentException(
                    string.Create(
                        System.Globalization.CultureInfo.InvariantCulture,
                        $"non increasing losses: {DataFormattersFunctions.Ordinal(i)} is {lossLevel[i - 1]}, {DataFormattersFunctions.Ordinal(i + 1)} is {lossLevel[i]}"),
                    nameof(lossLevel));
            }

            if (!(lossLevel[i] <= 1.0))
            {
                throw new ArgumentException(
                    string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Loss level {i} larger than 100% ({lossLevel[i]})"), nameof(lossLevel));
            }
        }
    }

    private static void CheckInputs(int nTrancheTenors, int nLosses, int volRows, int volsColumns)
    {
        if (nLosses != volRows)
        {
            throw new ArgumentException(
                $"mismatch between number of loss levels ({nLosses}) and number of rows ({volRows}) in the correl matrix",
                "correls");
        }

        if (nTrancheTenors != volsColumns)
        {
            throw new ArgumentException(
                $"mismatch between number of tranche tenors ({nTrancheTenors}) and number of columns ({volsColumns}) in the correl matrix",
                "correls");
        }
    }
}
