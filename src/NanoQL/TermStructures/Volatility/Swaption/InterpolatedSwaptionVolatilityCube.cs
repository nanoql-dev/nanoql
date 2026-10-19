// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2023 Ignacio Anguita
// SPDX-FileCopyrightText: Copyright (C) 2006 Katiuscia Manzoni
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/swaption/interpolatedswaptionvolatilitycube.hpp ql/termstructures/volatility/swaption/interpolatedswaptionvolatilitycube.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The swaption volatility cube that interpolates its volatility spreads: the fit-later, interpolate-early cube.
/// </summary>
/// <remarks>
/// <para>
/// Each strike spread's volatility spreads are interpolated bilinearly over the swap lengths and the option times,
/// extrapolating linearly. The smile section of an option date and a swap tenor is linear between the at-the-money
/// strike plus each strike spread, at the at-the-money volatility plus the spread interpolated there, from the section's
/// standard deviations on Actual/365 (Fixed); a section by option time rounds the swap length to whole months and
/// adjusts the option date to a fixing date of the swap index the tenor's at-the-money strike uses.
/// </para>
/// <para>
/// A section's at-the-money strike is a swap index's fixing, which QuantLib computes at the query under the global
/// evaluation date, fixings and at-par switch, and the volatility queries take no context. So the cube keeps the
/// context it was built with, and its sections read it: a query under another context still reads the build context.
/// Its results are QuantLib's whenever QuantLib's query runs under the settings it was built under.
/// </para>
/// <para>
/// QuantLib's cube computes its spread interpolations on its first query; here they are built with the cube, so a
/// refusal of the grid (a single option or swap tenor) comes at construction. QuantLib's <c>volSpreads(i)</c> reads
/// past its matrices for an index past the strike spreads; it is refused here.
/// </para>
/// </remarks>
public sealed class InterpolatedSwaptionVolatilityCube : SwaptionVolatilityCube
{
    private readonly EvaluationContext context;
    private readonly ImmutableArray<Matrix> volSpreadsMatrix;
    private readonly ImmutableArray<Interpolation2D> volSpreadsInterpolator;

    /// <summary>Initializes a new instance of the <see cref="InterpolatedSwaptionVolatilityCube"/> class.</summary>
    /// <param name="atmVolStructure">The at-the-money volatility structure.</param>
    /// <param name="optionTenors">The option tenors, positive and increasing; at least 2.</param>
    /// <param name="swapTenors">The swap tenors, positive, increasing, and in months or years; at least 2.</param>
    /// <param name="strikeSpreads">The strike spreads from the at-the-money strike, increasing; at least 2.</param>
    /// <param name="volSpreads">The volatility spreads: a row per option and swap tenor pair, the pair (j, k) in row
    /// j·(swap tenors) + k, with a spread per strike spread.</param>
    /// <param name="swapIndexBase">The swap index whose family and conventions give the at-the-money strikes of the swap
    /// tenors past the short index's.</param>
    /// <param name="shortSwapIndexBase">The swap index whose family and conventions give the at-the-money strikes of the
    /// other swap tenors.</param>
    /// <param name="vegaWeightedSmileFit">QuantLib's flag, which this cube doesn't read.</param>
    /// <param name="context">The evaluation context, which the cube's sections read for their at-the-money
    /// strikes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/>, the structure, a collection or an index is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The cube's base refuses its arguments, or there are fewer than 2 option or
    /// swap tenors.</exception>
    /// <exception cref="NanoQLException">Two tenors can't be compared, or the structure has no calendar.</exception>
    public InterpolatedSwaptionVolatilityCube(
        SwaptionVolatilityStructure atmVolStructure,
        IEnumerable<Period> optionTenors,
        IEnumerable<Period> swapTenors,
        IEnumerable<double> strikeSpreads,
        IEnumerable<IEnumerable<double>> volSpreads,
        SwapIndex swapIndexBase,
        SwapIndex shortSwapIndexBase,
        bool vegaWeightedSmileFit,
        EvaluationContext context)
        : base(
            EntryChecked(atmVolStructure, context),
            optionTenors,
            swapTenors,
            strikeSpreads,
            volSpreads,
            swapIndexBase,
            shortSwapIndexBase,
            vegaWeightedSmileFit,
            2)
    {
        this.context = context;

        // QuantLib's performCalculations: the spread matrices, a row per option tenor, then their interpolations.
        ImmutableArray<ImmutableArray<double>> spreads = VolSpreads();
        var matrices = new Matrix[NStrikes];
        var interpolators = new Interpolation2D[NStrikes];
        for (int i = 0; i < NStrikes; i++)
        {
            matrices[i] = new Matrix(NOptionTenors, NSwapTenors, 0.0);
            for (int j = 0; j < NOptionTenors; j++)
            {
                for (int k = 0; k < NSwapTenors; k++)
                {
                    matrices[i][j, k] = spreads[(j * NSwapTenors) + k][i];
                }
            }
        }

        for (int i = 0; i < NStrikes; i++)
        {
            interpolators[i] = new BilinearInterpolation(SwapLengths.AsSpan(), OptionTimes.AsSpan(), matrices[i])
                .EnableExtrapolation();
        }

        volSpreadsMatrix = [.. matrices];
        volSpreadsInterpolator = [.. interpolators];
    }

    /// <summary>A strike spread's volatility spreads.</summary>
    /// <param name="i">The strike spread's index.</param>
    /// <returns>A copy of the spreads: a row per option tenor and a column per swap tenor.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> is negative, or not less than the number of
    /// strike spreads.</exception>
    public Matrix VolSpreads(int i)
    {
        // QuantLib reads past its matrices for an index past them.
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(i, NStrikes);
        return volSpreadsMatrix[i].Clone();
    }

    /// <summary>
    /// The smile section of an option time and a swap length: the section of the option date interpolated from the
    /// time, adjusted to a fixing date, and of the swap length rounded to whole months.
    /// </summary>
    /// <param name="optionTime">The option time.</param>
    /// <param name="swapLength">The swap length, in years.</param>
    /// <returns>The smile section, under the context the cube was built with.</returns>
    protected override SmileSection SmileSectionImpl(double optionTime, double swapLength)
    {
        Date optionDate = OptionDateFromTime(optionTime);
        var rounder = new ClosestRounding(0);

        // QuantLib's static_cast<Integer>: MSVC's conversion gives the smallest integer for a value outside the
        // integers, as OptionDateFromTime takes it.
        double months = rounder.Evaluate(swapLength * 12.0);
        var swapTenor = new Period(
            months > -2147483649.0 && months < 2147483648.0 ? (int)months : int.MinValue, TimeUnit.Months);

        // ensure that option date is valid fixing date
        optionDate =
            swapTenor > ShortSwapIndexBase.Tenor
                ? SwapIndexBase.FixingCalendar.Adjust(optionDate, BusinessDayConvention.Following)
                : ShortSwapIndexBase.FixingCalendar.Adjust(optionDate, BusinessDayConvention.Following);
        return SmileSectionImpl(optionDate, swapTenor);
    }

    /// <summary>
    /// The smile section of an option date and a swap tenor: linear between the at-the-money strike plus each strike
    /// spread, at the at-the-money volatility plus the spread interpolated there.
    /// </summary>
    /// <param name="optionDate">The option date.</param>
    /// <param name="swapTenor">The swap tenor.</param>
    /// <returns>The smile section, under the context the cube was built with.</returns>
    protected override SmileSection SmileSectionImpl(Date optionDate, Period swapTenor)
    {
        double atmForward = AtmStrike(optionDate, swapTenor, context);
        double atmVol = AtmVol.Volatility(optionDate, swapTenor, atmForward);
        double optionTime = TimeFromReference(optionDate);
        double exerciseTimeSqrt = Math.Sqrt(optionTime);
        var strikes = new double[NStrikes];
        var stdDevs = new double[NStrikes];
        double length = SwapLength(swapTenor);
        ImmutableArray<double> strikeSpreads = StrikeSpreads;
        for (int i = 0; i < NStrikes; ++i)
        {
            strikes[i] = atmForward + strikeSpreads[i];
            stdDevs[i] = exerciseTimeSqrt * (atmVol + volSpreadsInterpolator[i].Evaluate(length, optionTime));
        }

        double shift = AtmVol.Shift(optionTime, length);
        return new InterpolatedSmileSection<Linear>(
            optionTime, strikes, stdDevs, atmForward, default, new Actual365Fixed(), VolatilityType, shift);
    }

    // The context's check first, as every member taking it checks it, before the base's.
    private static SwaptionVolatilityStructure EntryChecked(SwaptionVolatilityStructure atmVolStructure, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return atmVolStructure;
    }
}
