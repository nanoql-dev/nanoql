// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2009 Bernd Engelmann
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/inflation/yoycapfloortermpricesurface.hpp ql/experimental/inflation/yoycapfloortermpricesurface.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.Experimental;

/// <summary>
/// A year-on-year inflation cap and floor price surface interpolated over maturity times and strikes, its at-the-money
/// swap rates where the cap and floor prices meet, and its year-on-year curve bootstrapped on them: QuantLib's
/// <c>InterpolatedYoYCapFloorTermPriceSurface</c>.
/// </summary>
/// <typeparam name="TInterpolator2D">The 2-D interpolator over the maturity times and strikes.</typeparam>
/// <typeparam name="TInterpolator1D">The interpolator of the at-the-money swap rates over the maturity times.</typeparam>
/// <remarks>
/// <para>
/// At each maturity the at-the-money swap rate is the strike where the interpolated cap and floor prices meet, searched
/// in steps of 0.5% from the last floor strike and solved by Brent to 1e-7; a maturity with no meeting point within
/// 2%, or one below the arbitrage-free bound, takes the bound plus 0.1% (or the bounds' midpoint), and past five
/// years either refuses. The year-on-year curve is bootstrapped linearly on year-on-year swap helpers every year from
/// the nominal curve's reference date to the last maturity, each quoting the swap rate at its maturity, then checked to
/// reprice them to 1e-5. Everything is computed at construction (<see cref="Create"/>), the curve's helpers dated and
/// priced in the context given, as QuantLib computes at construction and again on a change.
/// </para>
/// <para>
/// Two fixes (the S-xrt plan's XRR5 (xii) and (xiv); <c>Divergences/YoYCapFloorPriceSurfaceTests.cs</c>): each swap
/// rate's date is its own maturity's option date, where QuantLib's takes the date of the maturity counted among the valid
/// ones, another maturity's once one fails the intersection (<c>yoycapfloortermpricesurface.hpp:506-508</c>); and the
/// repricing check reads each helper's own maturity, where QuantLib's reads the surface's reference date plus whole
/// years while the helpers sit on the nominal curve's. With every maturity valid and the two reference dates equal,
/// both are QuantLib's.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public class InterpolatedYoYCapFloorTermPriceSurface<TInterpolator2D, TInterpolator1D> : YoYCapFloorTermPriceSurface
    where TInterpolator2D : struct, IInterpolator2D
    where TInterpolator1D : struct, IInterpolator
{
    // QuantLib's constants (its TODO: "define the constants outside the code")
    private const double MaxSearchRange = 0.0201;
    private const double MaxExtrapolationMaturity = 5.01;
    private const double SearchStep = 0.0050;
    private const double IntrinsicValueAddOn = 0.001;
    private const double SolverTolerance = 1e-7;

    private readonly ImmutableArray<double> cfMaturityTimes;
    private readonly Interpolation2D capPrice;
    private readonly Interpolation2D floorPrice;
    private readonly ImmutableArray<double> atmTimes;
    private readonly ImmutableArray<Date> atmDates;
    private readonly ImmutableArray<double> atmRates;
    private readonly Interpolation atmYoYSwapRateCurve;
    private readonly YoYInflationTermStructure yoy;

    private protected InterpolatedYoYCapFloorTermPriceSurface(
        Date referenceDate,
        int fixingDays,
        Period yyLag,
        YoYInflationIndex yii,
        CPIInterpolationType interpolation,
        YieldTermStructure nominal,
        DayCounter dc,
        Calendar cal,
        BusinessDayConvention bdc,
        IReadOnlyList<double> cStrikes,
        IReadOnlyList<double> fStrikes,
        IReadOnlyList<Period> cfMaturities,
        Matrix cPrice,
        Matrix fPrice,
        TInterpolator2D interpolator2d,
        TInterpolator1D interpolator1d,
        EvaluationContext context)
        : base(
            referenceDate, fixingDays, yyLag, yii, interpolation, nominal, dc, cal, bdc, cStrikes, fStrikes, cfMaturities,
            cPrice, fPrice)
    {
        ImmutableArray<Period> maturities = Maturities;
        cfMaturityTimes = [.. maturities.Select(m => TimeFromReference(YoYOptionDateFromTenor(m)))];
        capPrice = interpolator2d.Interpolate(cfMaturityTimes.AsSpan(), CapStrikes.AsSpan(), CapPrices())
            .EnableExtrapolation();
        floorPrice = interpolator2d.Interpolate(cfMaturityTimes.AsSpan(), FloorStrikes.AsSpan(), FloorPrices())
            .EnableExtrapolation();
        (atmTimes, atmDates, atmRates) = Intersect();
        atmYoYSwapRateCurve = interpolator1d.Interpolate(atmTimes.AsSpan(), atmRates.AsSpan());
        yoy = CalculateYoYTermStructure(context);
    }

    /// <inheritdoc/>
    /// <remarks>The year-on-year curve's.</remarks>
    public override Date MaxDate => yoy.MaxDate;

    /// <summary>
    /// Builds the surface on the evaluation date's reference date, its year-on-year curve bootstrapped in the context.
    /// </summary>
    /// <param name="fixingDays">The fixing days.</param>
    /// <param name="yyLag">The quoted instruments' observation lag.</param>
    /// <param name="yii">The year-on-year index.</param>
    /// <param name="interpolation">How the index is observed.</param>
    /// <param name="nominal">The nominal curve.</param>
    /// <param name="dc">The day counter.</param>
    /// <param name="cal">The calendar, which adjusts the reference date and the helpers' payments.</param>
    /// <param name="bdc">The business-day convention of the helpers' payments.</param>
    /// <param name="cStrikes">The cap strikes, increasing.</param>
    /// <param name="fStrikes">The floor strikes, increasing.</param>
    /// <param name="cfMaturities">The maturities, increasing.</param>
    /// <param name="cPrice">The cap prices, a row per cap strike and a column per maturity, non-increasing in
    /// strike.</param>
    /// <param name="fPrice">The floor prices, a row per floor strike and a column per maturity, non-decreasing in
    /// strike.</param>
    /// <param name="context">The evaluation context: its evaluation date, and the market the helpers are priced
    /// in.</param>
    /// <param name="interpolator2d">The 2-D interpolator.</param>
    /// <param name="interpolator1d">The swap rates' interpolator.</param>
    /// <returns>The surface.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its enum.</exception>
    /// <exception cref="NanoQLException">The data are too few, mis-sized, non-positive or not monotonic; an
    /// intersection past five years fails; or the curve can't be bootstrapped or doesn't reprice its helpers; with
    /// QuantLib's texts.</exception>
    public static InterpolatedYoYCapFloorTermPriceSurface<TInterpolator2D, TInterpolator1D> Create(
        int fixingDays,
        Period yyLag,
        YoYInflationIndex yii,
        CPIInterpolationType interpolation,
        YieldTermStructure nominal,
        DayCounter dc,
        Calendar cal,
        BusinessDayConvention bdc,
        IReadOnlyList<double> cStrikes,
        IReadOnlyList<double> fStrikes,
        IReadOnlyList<Period> cfMaturities,
        Matrix cPrice,
        Matrix fPrice,
        EvaluationContext context,
        TInterpolator2D interpolator2d = default,
        TInterpolator1D interpolator1d = default)
    {
        ArgumentNullException.ThrowIfNull(cal);
        ArgumentNullException.ThrowIfNull(context);
        Date referenceDate = cal.Advance(context.EvaluationDate, 0, TimeUnit.Days);
        return new InterpolatedYoYCapFloorTermPriceSurface<TInterpolator2D, TInterpolator1D>(
            referenceDate, fixingDays, yyLag, yii, interpolation, nominal, dc, cal, bdc, cStrikes, fStrikes,
            cfMaturities, cPrice, fPrice, interpolator2d, interpolator1d, context);
    }

    /// <inheritdoc/>
    /// <remarks>The year-on-year curve's.</remarks>
    public override Date BaseDate() => yoy.BaseDate;

    /// <inheritdoc/>
    public override (ImmutableArray<double> Times, ImmutableArray<double> Rates) AtmYoYSwapTimeRates() =>
        (atmTimes, atmRates);

    /// <inheritdoc/>
    public override (ImmutableArray<Date> Dates, ImmutableArray<double> Rates) AtmYoYSwapDateRates() =>
        (atmDates, atmRates);

    /// <inheritdoc/>
    public override YoYInflationTermStructure YoYTS() => yoy;

    /// <inheritdoc/>
    public override double Price(Date d, double k)
    {
        double atm = AtmYoYSwapRate(d);
        return k > atm ? CapPrice(d, k) : FloorPrice(d, k);
    }

    /// <inheritdoc/>
    public override double FloorPrice(Date d, double k) => floorPrice.Evaluate(TimeFromReference(d), k);

    /// <inheritdoc/>
    public override double CapPrice(Date d, double k) => capPrice.Evaluate(TimeFromReference(d), k);

    /// <inheritdoc/>
    public override double AtmYoYSwapRate(Date d, bool extrapolate = true) =>
        atmYoYSwapRateCurve.Evaluate(TimeFromReference(d), extrapolate);

    /// <inheritdoc/>
    public override double AtmYoYRate(Date d, Period? obsLag = null, bool extrapolate = true)
    {
        // work in terms of maturity-of-instruments
        // so ask for rate with observation lag
        Period p = obsLag is { } lag && lag != new Period(-1, TimeUnit.Days) ? lag : ObservationLag;
        return yoy.YoyRate(d - p, extrapolate);
    }

    // QuantLib's intersect(): the at-the-money swap rates where the cap and floor prices meet.
    private (ImmutableArray<double> Times, ImmutableArray<Date> Dates, ImmutableArray<double> Rates) Intersect()
    {
        ImmutableArray<Period> maturities = Maturities;
        ImmutableArray<double> fStrikes = FloorStrikes;
        ImmutableArray<double> cStrikes = CapStrikes;
        var validMaturity = new bool[maturities.Length];
        var solver = new Brent();
        double[] minSwapRateIntersection = new double[cfMaturityTimes.Length];
        double[] maxSwapRateIntersection = new double[cfMaturityTimes.Length];
        var tmpSwapMaturities = new List<double>();
        var tmpSwapRates = new List<double>();
        for (int i = 0; i < maturities.Length; i++)
        {
            double t = cfMaturityTimes[i];

            // determine the sum of discount factors
            int numYears = (int)Math.Round(t, MidpointRounding.AwayFromZero);
            double sumDiscount = 0.0;
            for (int j = 0; j < numYears; ++j)
            {
                sumDiscount += NominalTermStructure.Discount(j + 1.0);
            }

            // determine the minimum value of the ATM swap point
            double tmpMinSwapRateIntersection = -1.0e10;
            double tmpMaxSwapRateIntersection = 1.0e10;
            for (int j = 0; j < fStrikes.Length; ++j)
            {
                double price = floorPrice.Evaluate(t, fStrikes[j]);
                double minSwapRate = fStrikes[j] - (price / (sumDiscount * 10000));
                if (minSwapRate > tmpMinSwapRateIntersection)
                {
                    tmpMinSwapRateIntersection = minSwapRate;
                }
            }

            for (int j = 0; j < cStrikes.Length; ++j)
            {
                double price = capPrice.Evaluate(t, cStrikes[j]);
                double maxSwapRate = cStrikes[j] + (price / (sumDiscount * 10000));
                if (maxSwapRate < tmpMaxSwapRateIntersection)
                {
                    tmpMaxSwapRateIntersection = maxSwapRate;
                }
            }

            maxSwapRateIntersection[i] = tmpMaxSwapRateIntersection;
            minSwapRateIntersection[i] = tmpMinSwapRateIntersection;

            // find the interval where the intersection lies
            bool trialsExceeded = false;
            int numTrials = (int)(MaxSearchRange / SearchStep);
            double lo;
            double hi;
            if (floorPrice.Evaluate(t, fStrikes[^1]) > capPrice.Evaluate(t, fStrikes[^1]))
            {
                int counter = 1;
                bool stop = false;
                double strike = 0.0;
                while (!stop)
                {
                    strike = fStrikes[^1] - (counter * SearchStep);
                    if (floorPrice.Evaluate(t, strike) < capPrice.Evaluate(t, strike))
                    {
                        stop = true;
                    }

                    counter++;
                    if (counter == numTrials + 1 && !stop)
                    {
                        stop = true;
                        trialsExceeded = true;
                    }
                }

                lo = strike;
                hi = strike + SearchStep;
            }
            else
            {
                int counter = 1;
                bool stop = false;
                double strike = 0.0;
                while (!stop)
                {
                    strike = fStrikes[^1] + (counter * SearchStep);
                    if (floorPrice.Evaluate(t, strike) > capPrice.Evaluate(t, strike))
                    {
                        stop = true;
                    }

                    counter++;
                    if (counter == numTrials + 1 && !stop)
                    {
                        stop = true;
                        trialsExceeded = true;
                    }
                }

                lo = strike - SearchStep;
                hi = strike;
            }

            double guess = (hi + lo) / 2.0;
            if (!trialsExceeded)
            {
                double kI;
                try
                {
                    // allow extrapolation because the overlap is typically insufficient
                    // looking for a zero
                    double tt = t;
                    kI = solver.Solve(
                        g => capPrice.Evaluate(tt, g, true) - floorPrice.Evaluate(tt, g, true),
                        SolverTolerance,
                        guess,
                        lo,
                        hi);
                }
                catch (NanoQLException e)
                {
                    throw new NanoQLException(
                        $"cap/floor intersection finding failed at t = {Show(t)}, error msg: {e.Message}", e);
                }

                // error message if kI is economically nonsensical (only if t is large)
                if (kI <= minSwapRateIntersection[i])
                {
                    if (t > MaxExtrapolationMaturity)
                    {
                        throw new NanoQLException(
                            $"cap/floor intersection finding failed at t = {Show(t)}, error msg: intersection value is " +
                            $"below the arbitrage free lower bound {Show(minSwapRateIntersection[i])}");
                    }
                }
                else
                {
                    tmpSwapMaturities.Add(t);
                    tmpSwapRates.Add(kI);
                    validMaturity[i] = true;
                }
            }
            else
            {
                // error message if t is too large
                if (t > MaxExtrapolationMaturity)
                {
                    throw new NanoQLException(
                        $"cap/floor intersection finding failed at t = {Show(t)}, error msg: no interection found " +
                        "inside the admissible range");
                }
            }
        }

        var times = ImmutableArray.CreateBuilder<double>(maturities.Length);
        var dates = ImmutableArray.CreateBuilder<Date>(maturities.Length);
        var rates = ImmutableArray.CreateBuilder<double>(maturities.Length);
        int valid = 0;
        for (int i = 0; i < maturities.Length; ++i)
        {
            if (!validMaturity[i])
            {
                dates.Add(ReferenceDate + maturities[i]);
                times.Add(TimeFromReference(ReferenceDate + maturities[i]));

                // Heuristic: overwrite the swap rate with a value that guarantees that the
                // intrinsic value of all options is lower than the price
                double newSwapRate = minSwapRateIntersection[i] + IntrinsicValueAddOn;
                if (newSwapRate > maxSwapRateIntersection[i])
                {
                    newSwapRate = 0.5 * (minSwapRateIntersection[i] + maxSwapRateIntersection[i]);
                }

                rates.Add(newSwapRate);
            }
            else
            {
                // the maturity's own date (XRR5 (xii)), where QuantLib's takes the valid count's
                times.Add(tmpSwapMaturities[valid]);
                rates.Add(tmpSwapRates[valid]);
                dates.Add(YoYOptionDateFromTenor(maturities[i]));
                valid++;
            }
        }

        return (times.MoveToImmutable(), dates.MoveToImmutable(), rates.MoveToImmutable());
    }

    // QuantLib's calculateYoYTermStructure(): the curve bootstrapped on a helper a year from the nominal curve's
    // reference date to the last maturity, checked to reprice them.
    private PiecewiseYoYInflation CalculateYoYTermStructure(EvaluationContext context)
    {
        // which yoy-swap points to use in building the yoy-fwd curve?
        // for now pick every year
        int nYears = (int)Math.Round(TimeFromReference(ReferenceDate + Maturities[^1]), MidpointRounding.AwayFromZero);
        var maturities = new List<Date>(nYears);
        var yyHelpers = new List<BootstrapHelper<YoYInflationTermStructure>>(nYears);
        for (int i = 1; i <= nYears; i++)
        {
            Date maturity = NominalTermStructure.ReferenceDate + new Period(i, TimeUnit.Years);
            maturities.Add(maturity);
            yyHelpers.Add(new YearOnYearInflationSwapHelper(
                AtmYoYSwapRate(maturity),
                ObservationLag,
                maturity,
                Calendar,
                BusinessDayConvention,
                DayCounter,
                YoYIndex,
                IndexIsInterpolated ? CPIInterpolationType.Linear : CPIInterpolationType.Flat,
                NominalTermStructure,
                context));
        }

        Date baseDate = InflationTermStructureFunctions.InflationPeriod(
            NominalTermStructure.ReferenceDate - ObservationLag, YoYIndex.Frequency).Start;

        // usually this base rate is known
        // however for the data to be self-consistent
        // we pick this as the end of the curve
        double baseYoYRate = AtmYoYSwapRate(ReferenceDate);

        // Linear is OK because we have every year
        var pYITS = new PiecewiseYoYInflation(
            NominalTermStructure.ReferenceDate, baseDate, baseYoYRate, YoYIndex.Frequency, DayCounter, yyHelpers, context);

        // check that helpers are repriced, each at its own maturity (XRR5 (xiv))
        const double Eps = 1e-5;
        for (int i = 0; i < yyHelpers.Count; i++)
        {
            double original = AtmYoYSwapRate(maturities[i]);
            double implied = yyHelpers[i].SetTermStructure(pYITS).ImpliedQuote(context);
            if (!(Math.Abs(implied - original) < Eps))
            {
                throw new NanoQLException(
                    $"could not reprice helper {i}, data {Show(original)}, implied quote {Show(implied)}");
            }
        }

        return pYITS;
    }

    private static string Show(double x) => DataFormattersFunctions.General(x);
}
