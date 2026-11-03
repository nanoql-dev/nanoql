// SPDX-FileCopyrightText: Copyright (C) 2010, 2011 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2010 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/inflation/cpicapfloortermpricesurface.hpp ql/experimental/inflation/cpicapfloortermpricesurface.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A CPI cap and floor price surface interpolated over maturity times and quoted strikes: QuantLib's
/// <c>InterpolatedCPICapFloorTermPriceSurface</c>.
/// </summary>
/// <typeparam name="TInterpolator2D">The 2-D interpolator over the maturity times and strikes.</typeparam>
/// <remarks>
/// <para>
/// At each maturity the quoted prices fill every strike by put-call parity, a floor's price giving the cap's at its
/// strike and the reverse: C − F = (A − K)·D, with A and K the at-the-money rate and the strike compounded over the
/// maturity in years, (1 + r)^T, and D the nominal discount factor to the option date, the reference date plus the
/// maturity adjusted.
/// </para>
/// <para>
/// QuantLib compounds over the maturity's length in its own unit, so an 18-month maturity compounds over 18 years; here
/// over 1.5, QuantLib's own <c>years</c> of the period, its bits on maturities in years, and a maturity in weeks or
/// days, which has no length in years, is refused (<c>Divergences/CPICapFloorPriceSurfaceTests.cs</c>).
/// </para>
/// <para>
/// The at-the-money rates read the index's fixings, so the surface is built from a context (<see cref="Create"/>), as
/// QuantLib's is built from the global settings' at its first read; QuantLib recomputes on a change, and here a new
/// market builds a new surface. A price past the last maturity or strike extrapolates, as QuantLib's interpolations,
/// which enable it.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class InterpolatedCPICapFloorTermPriceSurface<TInterpolator2D> : CPICapFloorTermPriceSurface
    where TInterpolator2D : struct, IInterpolator2D
{
    private readonly Matrix cPriceB;
    private readonly Matrix fPriceB;
    private readonly ImmutableArray<double> cfMaturityTimes;
    private readonly Interpolation2D capPrice;
    private readonly Interpolation2D floorPrice;

    private InterpolatedCPICapFloorTermPriceSurface(
        Date referenceDate,
        double nominal,
        double startRate,
        Period observationLag,
        Calendar cal,
        BusinessDayConvention bdc,
        DayCounter dc,
        ZeroInflationIndex zii,
        CPIInterpolationType interpolationType,
        YieldTermStructure yts,
        IReadOnlyList<double> cStrikes,
        IReadOnlyList<double> fStrikes,
        IReadOnlyList<Period> cfMaturities,
        Matrix cPrice,
        Matrix fPrice,
        TInterpolator2D interpolator2d,
        EvaluationContext context)
        : base(
            referenceDate, nominal, startRate, observationLag, cal, bdc, dc, zii, interpolationType, yts, cStrikes,
            fStrikes, cfMaturities, cPrice, fPrice)
    {
        ImmutableArray<double> strikes = Strikes;
        ImmutableArray<Period> maturities = Maturities;
        ImmutableArray<double> capStrikes = CapStrikes;
        ImmutableArray<double> floorStrikes = FloorStrikes;
        Matrix quotedCaps = CapPrices();
        Matrix quotedFloors = FloorPrices();
        cPriceB = new Matrix(strikes.Length, maturities.Length, double.NaN);
        fPriceB = new Matrix(strikes.Length, maturities.Length, double.NaN);
        for (int j = 0; j < maturities.Length; ++j)
        {
            Period mat = maturities[j];
            double years = PeriodFunctions.Years(mat);
            double df = NominalTermStructure.Discount(CpiOptionDateFromTenor(mat));
            double atmQuote = AtmRate(CpiOptionDateFromTenor(mat), context);
            double atm = Math.Pow(1.0 + atmQuote, years);
            double s = atm * df;
            for (int i = 0; i < strikes.Length; ++i)
            {
                double kQuote = strikes[i];
                double k = Math.Pow(1.0 + kQuote, years);
                int indF = IndexOfClose(floorStrikes, kQuote);
                int indC = IndexOfClose(capStrikes, kQuote);
                bool isFloorStrike = indF >= 0;
                bool isCapStrike = indC >= 0;
                if (isFloorStrike)
                {
                    fPriceB[i, j] = quotedFloors[indF, j];
                    if (!isCapStrike)
                    {
                        cPriceB[i, j] = quotedFloors[indF, j] + s - (k * df);
                    }
                }

                if (isCapStrike)
                {
                    cPriceB[i, j] = quotedCaps[indC, j];
                    if (!isFloorStrike)
                    {
                        fPriceB[i, j] = quotedCaps[indC, j] + (k * df) - s;
                    }
                }
            }
        }

        // every cell is filled: each strike is a floor strike or a cap strike (QuantLib's unexpected-case checks)
        cfMaturityTimes = [.. maturities.Select(m => TimeFromReference(CpiOptionDateFromTenor(m)))];
        capPrice = interpolator2d.Interpolate(cfMaturityTimes.AsSpan(), strikes.AsSpan(), cPriceB).EnableExtrapolation();
        floorPrice = interpolator2d.Interpolate(cfMaturityTimes.AsSpan(), strikes.AsSpan(), fPriceB).EnableExtrapolation();
    }

    /// <summary>
    /// Builds the surface on the evaluation date's reference date, its prices filled across the strikes at the context's
    /// fixings.
    /// </summary>
    /// <param name="nominal">The prices' nominal.</param>
    /// <param name="startRate">The at-the-money rate at the reference date, where no inflation time has passed.</param>
    /// <param name="observationLag">The quoted instruments' observation lag.</param>
    /// <param name="cal">The calendar adjusting option dates and the reference date.</param>
    /// <param name="bdc">The convention adjusting option dates.</param>
    /// <param name="dc">The day counter.</param>
    /// <param name="zii">The zero-inflation index, which must hold a zero-inflation curve.</param>
    /// <param name="interpolationType">How the index is observed.</param>
    /// <param name="yts">The nominal curve.</param>
    /// <param name="cStrikes">The cap strikes, quoted, increasing.</param>
    /// <param name="fStrikes">The floor strikes, quoted, increasing.</param>
    /// <param name="cfMaturities">The maturities, increasing.</param>
    /// <param name="cPrice">The cap prices, a row per cap strike and a column per maturity, non-increasing in
    /// strike.</param>
    /// <param name="fPrice">The floor prices, a row per floor strike and a column per maturity, non-decreasing in
    /// strike.</param>
    /// <param name="context">The evaluation context: its evaluation date, and the fixings the at-the-money rates
    /// read.</param>
    /// <param name="interpolator2d">The 2-D interpolator.</param>
    /// <returns>The surface.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its enum.</exception>
    /// <exception cref="NanoQLException">The index has no zero-inflation curve, the data are too few, mis-sized,
    /// non-positive or not monotonic, or a fixing can't be read or forecast, with QuantLib's texts.</exception>
    public static InterpolatedCPICapFloorTermPriceSurface<TInterpolator2D> Create(
        double nominal,
        double startRate,
        Period observationLag,
        Calendar cal,
        BusinessDayConvention bdc,
        DayCounter dc,
        ZeroInflationIndex zii,
        CPIInterpolationType interpolationType,
        YieldTermStructure yts,
        IReadOnlyList<double> cStrikes,
        IReadOnlyList<double> fStrikes,
        IReadOnlyList<Period> cfMaturities,
        Matrix cPrice,
        Matrix fPrice,
        EvaluationContext context,
        TInterpolator2D interpolator2d = default)
    {
        ArgumentNullException.ThrowIfNull(cal);
        ArgumentNullException.ThrowIfNull(context);
        Date referenceDate = cal.Advance(context.EvaluationDate, 0, TimeUnit.Days);
        return new InterpolatedCPICapFloorTermPriceSurface<TInterpolator2D>(
            referenceDate, nominal, startRate, observationLag, cal, bdc, dc, zii, interpolationType, yts, cStrikes,
            fStrikes, cfMaturities, cPrice, fPrice, interpolator2d, context);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="NanoQLException">A fixing can't be read or forecast.</exception>
    public override double Price(Date d, double k, EvaluationContext context)
    {
        double atm = AtmRate(d, context);
        return k > atm ? CapPrice(d, k) : FloorPrice(d, k);
    }

    /// <inheritdoc/>
    public override double CapPrice(Date d, double k) => capPrice.Evaluate(TimeFromReference(d), k);

    /// <inheritdoc/>
    public override double FloorPrice(Date d, double k) => floorPrice.Evaluate(TimeFromReference(d), k);

    // QuantLib's find_if with close_enough over the quoted strikes: the first close one, -1 for none.
    private static int IndexOfClose(ImmutableArray<double> strikes, double k)
    {
        for (int i = 0; i < strikes.Length; ++i)
        {
            if (ComparisonFunctions.CloseEnough(strikes[i], k))
            {
                return i;
            }
        }

        return -1;
    }
}
