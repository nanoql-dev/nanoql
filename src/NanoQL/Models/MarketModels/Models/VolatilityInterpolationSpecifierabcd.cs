// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/models/volatilityinterpolationspecifierabcd.hpp ql/models/marketmodels/models/volatilityinterpolationspecifierabcd.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// The volatility interpolation of synthetic short rates on abcd curves: each long rate's a, b and d scaled, each
/// short rate between two long ones on the mean of their a, b, c and d, those before the first and after the last on
/// the nearest long rate's, the last rescaled to match the last caplet's volatility.
/// </summary>
/// <remarks>
/// <para>
/// Immutable (the S-mm plan's MR2 (c)): <see cref="WithScalingFactors"/> and <see cref="WithLastCapletVol"/> return a
/// new specifier, where QuantLib's setters change it in place.
/// </para>
/// <para>
/// One departure from QuantLib (<c>Divergences/VolatilityInterpolationSpecifierOffsetTests.cs</c>): a short rate
/// between two long rates resets at its own index, <c>i + j·period + offset</c>, where QuantLib's resets at
/// <c>i + j·period</c>, an earlier rate's, at an offset other than 0 (MR3 (iii);
/// <c>volatilityinterpolationspecifierabcd.cpp:169-170</c>). A period of 0, which QuantLib divides by, no long rates,
/// whose last QuantLib reads, no short rate times, and long rates' times past the short ones, which QuantLib reads
/// past, are refused (MR3 (xx)).
/// </para>
/// </remarks>
public sealed class VolatilityInterpolationSpecifierabcd : VolatilityInterpolationSpecifier
{
    private readonly int period;
    private readonly int offset;
    private readonly ImmutableArray<PiecewiseConstantAbcdVariance> originalAbcdVariances;
    private readonly double lastCapletVol;
    private readonly ImmutableArray<double> timesForSmallRates;
    private readonly ImmutableArray<double> scalingFactors;
    private readonly int noBigRates;
    private readonly int noSmallRates;

    /// <summary>Initializes a new instance of the <see cref="VolatilityInterpolationSpecifierabcd"/> class.</summary>
    /// <param name="period">The short rates each long rate spans, at least 1.</param>
    /// <param name="offset">The short rates before the first long rate.</param>
    /// <param name="originalVariances">The long rates' abcd variances, at least one, on every period-th short rate
    /// time.</param>
    /// <param name="timesForSmallRates">The short rates' rate times.</param>
    /// <param name="lastCapletVol">The last caplet's volatility; 0 for the last long rate's own total
    /// volatility.</param>
    /// <exception cref="ArgumentException">
    /// The counts of long and short rates don't match the period and offset, a long rate's times aren't the short ones,
    /// or there are no long rates or short rate times.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="period"/> is below 1 or <paramref name="offset"/>
    /// negative.</exception>
    public VolatilityInterpolationSpecifierabcd(
        int period,
        int offset,
        ReadOnlySpan<PiecewiseConstantAbcdVariance> originalVariances,
        ReadOnlySpan<double> timesForSmallRates,
        double lastCapletVol = 0.0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        // QuantLib divides by the period, reads the last long rate and sizes its short rates from their times less one
        // (MR3 (xx)).
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);
        if (!(originalVariances.Length > 0))
        {
            throw new ArgumentException("at least one long rate's variance is required", nameof(originalVariances));
        }

        if (!(timesForSmallRates.Length > 0))
        {
            throw new ArgumentException("at least one short rate time is required", nameof(timesForSmallRates));
        }

        foreach (PiecewiseConstantAbcdVariance v in originalVariances)
        {
            ArgumentNullException.ThrowIfNull(v, nameof(originalVariances));
        }

        this.period = period;
        this.offset = offset;
        originalAbcdVariances = [.. originalVariances];
        this.timesForSmallRates = [.. timesForSmallRates];
        noBigRates = originalVariances.Length;
        noSmallRates = timesForSmallRates.Length - 1;
        scalingFactors = [.. Enumerable.Repeat(1.0, noBigRates)];

        if (!((noSmallRates - offset) / period == noBigRates))
        {
            throw new ArgumentException("size mismatch in VolatilityInterpolationSpecifierabcd", nameof(originalVariances));
        }

        for (int i = 0; i < noBigRates; ++i)
        {
            ImmutableArray<double> rateTimes = originalVariances[i].RateTimes;
            for (int j = 0; j < rateTimes.Length; ++j)
            {
                // QuantLib reads past the short rate times where a long rate has more times (MR3 (xx))
                int k = offset + (j * period);
                if (!(k < timesForSmallRates.Length && rateTimes[j] == timesForSmallRates[k]))
                {
                    throw new ArgumentException(
                        "rate times in variances passed in don't match small times in VolatilityInterpolationSpecifierabcd",
                        nameof(originalVariances));
                }
            }
        }

        this.lastCapletVol = lastCapletVol == 0.0
            ? originalVariances[noBigRates - 1].TotalVolatility(noBigRates - 1)
            : lastCapletVol;

        // change type of array to PiecewiseConstantVariance for client, from PiecewiseConstantAbcdVariance
        OriginalVariances = [.. originalAbcdVariances.Select(v => (PiecewiseConstantVariance)v)];
        InterpolatedVariances = Recompute();
    }

    private VolatilityInterpolationSpecifierabcd(
        VolatilityInterpolationSpecifierabcd other, ImmutableArray<double> scalingFactors, double lastCapletVol)
    {
        period = other.period;
        offset = other.offset;
        originalAbcdVariances = other.originalAbcdVariances;
        timesForSmallRates = other.timesForSmallRates;
        noBigRates = other.noBigRates;
        noSmallRates = other.noSmallRates;
        OriginalVariances = other.OriginalVariances;
        this.scalingFactors = scalingFactors;
        this.lastCapletVol = lastCapletVol;
        InterpolatedVariances = Recompute();
    }

    /// <inheritdoc/>
    public override ImmutableArray<PiecewiseConstantVariance> InterpolatedVariances { get; }

    /// <inheritdoc/>
    public override ImmutableArray<PiecewiseConstantVariance> OriginalVariances { get; }

    /// <inheritdoc/>
    public override int Period => period;

    /// <inheritdoc/>
    public override int Offset => offset;

    /// <inheritdoc/>
    public override int NoBigRates => noBigRates;

    /// <inheritdoc/>
    public override int NoSmallRates => noSmallRates;

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The factors aren't one per long rate.</exception>
    public override VolatilityInterpolationSpecifier WithScalingFactors(ReadOnlySpan<double> scales)
    {
        if (!(scalingFactors.Length == scales.Length))
        {
            throw new ArgumentException(
                "inappropriate number of scales passed in to VolatilityInterpolationSpecifierabcd::setScalingFactors ",
                nameof(scales));
        }

        return new VolatilityInterpolationSpecifierabcd(this, [.. scales], lastCapletVol);
    }

    /// <inheritdoc/>
    public override VolatilityInterpolationSpecifier WithLastCapletVol(double vol) =>
        new VolatilityInterpolationSpecifierabcd(this, scalingFactors, vol);

    private ImmutableArray<PiecewiseConstantVariance> Recompute()
    {
        var scaled = new PiecewiseConstantAbcdVariance[noBigRates];
        for (int i = 0; i < noBigRates; ++i)
        {
            (double a, double b, double c, double d) = originalAbcdVariances[i].GetABCD();
            a *= scalingFactors[i];
            b *= scalingFactors[i];

            // c is not scaled
            d *= scalingFactors[i];

            scaled[i] = new PiecewiseConstantAbcdVariance(a, b, c, d, i, originalAbcdVariances[i].RateTimes.AsSpan());
        }

        var interpolated = new PiecewiseConstantVariance[noSmallRates];

        // three cases: before offset, between offset and last big rate, and after last big rate

        // before offset
        {
            (double a, double b, double c, double d) = scaled[0].GetABCD();
            for (int i = 0; i < offset; ++i)
            {
                interpolated[i] = new PiecewiseConstantAbcdVariance(a, b, c, d, i, timesForSmallRates.AsSpan());
            }
        }

        // in between rates
        for (int j = 0; j < noBigRates - 1; ++j)
        {
            (double a0, double b0, double c0, double d0) = scaled[j].GetABCD();
            (double a1, double b1, double c1, double d1) = scaled[j + 1].GetABCD();
            double a = 0.5 * (a0 + a1);
            double b = 0.5 * (b0 + b1);
            double c = 0.5 * (c0 + c1);
            double d = 0.5 * (d0 + d1);

            for (int i = 0; i < period; ++i)
            {
                // the slot's own reset index, where QuantLib's is i + j·period (MR3 (iii))
                int slot = i + (j * period) + offset;
                interpolated[slot] = new PiecewiseConstantAbcdVariance(a, b, c, d, slot, timesForSmallRates.AsSpan());
            }
        }

        {
            (double a, double b, double c, double d) = scaled[noBigRates - 1].GetABCD();
            for (int i = offset + ((noBigRates - 1) * period); i < noSmallRates; ++i)
            {
                interpolated[i] = new PiecewiseConstantAbcdVariance(a, b, c, d, i, timesForSmallRates.AsSpan());
            }

            // very last rate is special as we must match the caplet vol
            double vol = interpolated[noSmallRates - 1].TotalVolatility(noSmallRates - 1);

            double scale = lastCapletVol / vol;
            a *= scale;
            b *= scale;
            d *= scale;
            interpolated[noSmallRates - 1] = new PiecewiseConstantAbcdVariance(a, b, c, d, noSmallRates - 1, timesForSmallRates.AsSpan());
        }

        return ImmutableArray.Create(interpolated);
    }
}
