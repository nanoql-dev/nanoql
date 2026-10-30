// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/inflation/interpolatedyoyoptionletstripper.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// Strips year-on-year optionlet volatilities along each strike by bootstrapping an interpolated curve on the price
/// surface's caps or floors: QuantLib's <c>InterpolatedYoYOptionletStripper</c>.
/// </summary>
/// <typeparam name="TInterpolator">The interpolator of each strike's curve in time.</typeparam>
/// <remarks>
/// <para>
/// Each strike is stripped on floors up to the last floor strike and on caps above it. Its first volatility is solved
/// by Brent, between 0.001% and 8% to 1e-7, so that a linear curve from the surface's base date to a week past its
/// first maturity, sloping by the slope in proportion to its level, prices the first maturity's quote; then a piecewise
/// curve starting at that level less the slope's change over the first maturity's time is bootstrapped on a helper per
/// maturity, each an integer number of years long, over a strike range of ±0.1% of the strike (at least 0.002%). The
/// instruments read a generic year-on-year index on the surface's year-on-year curve, with the surface's lag and
/// frequency, observed flat.
/// </para>
/// <para>
/// QuantLib's generic index takes the empty currency, which nothing reads; nanoql's inflation indexes take a currency,
/// so it takes the surface's index's. QuantLib binds each helper to a constant volatility at the first level before the
/// bootstrap, which only sets the shared engine's volatility that the bootstrap then resets; here each trial passes its
/// own volatility, so the binding isn't made.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class InterpolatedYoYOptionletStripper<TInterpolator> : YoYOptionletStripper
    where TInterpolator : struct, IInterpolator
{
    // The provider at the stripper's construction, which a strip builds its index, caps and curves in.
    private readonly ConventionProvider provider = ConventionProvider.Current;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="NanoQLException">A strike's first volatility can't be found ("failed to find solution here
    /// because: …"), the first maturity is under half a year, or a curve can't be bootstrapped, with QuantLib's
    /// texts.</exception>
    public override StrippedYoYOptionlets Strip(
        YoYCapFloorTermPriceSurface surface,
        Func<YoYInflationCapFloor, EvaluationContext, YoYOptionletVolatilitySurface, InstrumentResults> engine,
        double slope,
        EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(context);

        // built in the provider the stripper was built with
        using IDisposable scope = ConventionProvider.Use(provider);
        Period lag = surface.ObservationLag;
        Frequency frequency = surface.Frequency;
        bool indexIsInterpolated = surface.IndexIsInterpolated;
        int fixingDays = surface.FixingDays;
        const int SettlementDays = 0; // always
        Calendar cal = surface.Calendar!;
        BusinessDayConvention bdc = surface.BusinessDayConvention;
        DayCounter dc = surface.DayCounter;

        // switch from caps to floors when out of floors
        double maxFloor = surface.FloorStrikes[^1];
        YoYInflationCapFloorType useType = YoYInflationCapFloorType.Floor;
        Period tpMin = surface.Maturities[0];

        // create a "fake index" based on Generic, this should work
        // provided that the lag and frequency are correct
        var anIndex = new YYGenericCPI(frequency, false, lag, surface.YoYIndex.Currency, surface.YoYTS());

        // strip each K separatly
        var curves = new List<YoYOptionletVolatilitySurface>(surface.Strikes.Length);
        foreach (double k in surface.Strikes)
        {
            if (k > maxFloor)
            {
                useType = YoYInflationCapFloorType.Cap;
            }

            // solve for the initial point on the vol curve
            var solver = new Brent();
            const double SolverTolerance = 1e-7;

            // these are VOLATILITY guesses (always +)
            const double Lo = 0.00001;
            const double Hi = 0.08;
            const double Guess = (Hi + Lo) / 2.0;
            double priceToMatch = useType == YoYInflationCapFloorType.Cap
                ? surface.CapPrice(tpMin, k)
                : surface.FloorPrice(tpMin, k);
            double found;
            try
            {
                Func<double, double> objective = ObjectiveFunction(
                    useType, slope, k, lag, frequency, anIndex, surface, engine, priceToMatch, context);
                found = solver.Solve(objective, SolverTolerance, Guess, Lo, Hi);
            }
            catch (NanoQLException e)
            {
                throw new NanoQLException($"failed to find solution here because: {e.Message}", e);
            }

            // ***create helpers***
            const double Notional = 10000; // work in bps
            var helperInstruments = new List<BootstrapHelper<YoYOptionletVolatilitySurface>>();
            foreach (Period tp in surface.Maturities)
            {
                double nextPrice = useType == YoYInflationCapFloorType.Cap
                    ? surface.CapPrice(tp, k)
                    : surface.FloorPrice(tp, k);

                // helper should be an integer number of periods away,
                // this is enforced by rounding
                int nT = (int)Math.Floor(surface.TimeFromReference(surface.YoYOptionDateFromTenor(tp)) + 0.5);
                helperInstruments.Add(new YoYOptionletHelper(
                    nextPrice, Notional, useType, lag, dc, cal, fixingDays, anIndex, CPIInterpolationType.Flat, k, nT,
                    engine, context));
            }

            // ***bootstrap***
            // this is the artificial vol at zero so that first section works
            double tMin = surface.TimeFromReference(surface.YoYOptionDateFromTenor(tpMin));
            double baseYoYVolatility = found - (slope * tMin * found);
            double eps = Math.Max(k, 0.02) / 1000.0;
            double minStrike = k - eps;
            double maxStrike = k + eps;
            curves.Add(PiecewiseYoYOptionletVolatilityCurve<TInterpolator, IterativeBootstrap, YoYInflationVolatilityTraits>
                .Create(
                    SettlementDays, cal, bdc, dc, lag, frequency, indexIsInterpolated, minStrike, maxStrike,
                    baseYoYVolatility, helperInstruments, context));
        }

        return new StrippedYoYOptionlets(surface.Strikes, curves);
    }

    // QuantLib's ObjectiveFunction, used to set up the first point on each vol curve using assumptions on unobserved
    // vols at start: the quote less the cap or floor's price on a linear curve through the guess.
    private static Func<double, double> ObjectiveFunction(
        YoYInflationCapFloorType type,
        double slope,
        double k,
        Period lag,
        Frequency frequency,
        YoYInflationIndex anIndex,
        YoYCapFloorTermPriceSurface surf,
        Func<YoYInflationCapFloor, EvaluationContext, YoYOptionletVolatilitySurface, InstrumentResults> p,
        double priceToMatch,
        EvaluationContext context)
    {
        Period surfaceLag = surf.ObservationLag;
        YoYInflationCapFloor capfloor = new MakeYoYInflationCapFloor(
                type,
                anIndex,
                (int)Math.Floor(0.5 + surf.TimeFromReference(surf.MinMaturity)),
                surf.Calendar,
                lag,
                CPIInterpolationType.Flat)
            .WithNominal(10000.0)
            .WithStrike(k)
            .ToYoYInflationCapFloor(context);

        // shortest time available from price surface
        Date[] dvec = [surf.BaseDate(), surf.MinMaturity + new Period(7, TimeUnit.Days)];
        double[] tvec =
        [
            surf.DayCounter.YearFraction(surf.ReferenceDate, dvec[0]),
            surf.DayCounter.YearFraction(surf.ReferenceDate, dvec[1]),
        ];
        int n = (int)Math.Floor(0.5 + surf.TimeFromReference(surf.MinMaturity));
        if (!(n > 0))
        {
            throw new NanoQLException($"first maturity in price surface not > 0: {n}");
        }

        // QuantLib's indexIsInterpolated_ member, never set from the surface
        const bool IndexIsInterpolated = false;
        return guess =>
        {
            double[] vvec = [guess - (slope * (tvec[1] - tvec[0]) * guess), guess];

            // could have Interpolator1D instead of Linear
            var vCurve = InterpolatedYoYOptionletVolatilityCurve<Linear>.Create(
                0, new TARGET(), BusinessDayConvention.ModifiedFollowing, new Actual365Fixed(), surfaceLag, frequency,
                IndexIsInterpolated, dvec, vvec, -1.0, 3.0, context); // strike limits
            return priceToMatch - (p(capfloor, context, vCurve).NPV ?? throw new NanoQLException("NPV not provided"));
        };
    }
}
