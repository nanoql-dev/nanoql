// SPDX-FileCopyrightText: Copyright (C) 2025 Hiroto Ogawa
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/futures/discountingperpetualfuturesengine.hpp ql/pricingengines/futures/discountingperpetualfuturesengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The discounting engine for perpetual futures: the spot times the funding payments' discounted sum (a linear
/// payoff), or over it (an inverse payoff), the funding rates and interest-rate differences interpolated in time and
/// flat past the last, the sum to a horizon on a grid and its tail in closed form.
/// </summary>
/// <remarks>
/// <para>
/// Stateless. The grid's times are the instrument's day counter's from the evaluation date, and each curve is read at
/// the instants they denote, on its own clock, nothing accruing before its date (the S-exo plan's ER5 (xxix),
/// <c>Divergences/PerpetualFuturesMismatchedTimesTests.cs</c>), where QuantLib reads the curves at the grid's times
/// themselves; on curves counted and dated as the grid, QuantLib's reads.
/// </para>
/// <para>
/// The tail past the horizon converges only where the last funding rate k and the curves' last forward rates r_f and
/// r_d give k + r_f − r_d &gt; 0 (continuous funding) or (1 + k)·e^{Δt(r_f − r_d)} &gt; 1 with 1 + k &gt; 0 (discrete
/// funding); elsewhere it is refused, where QuantLib refuses only k ≤ 0 and returns the divergent series' analytic
/// continuation below (ER8, <c>Divergences/PerpetualFuturesDivergentTailTests.cs</c>). A non-positive last funding
/// rate on a convergent tail is priced.
/// </para>
/// </remarks>
public static class DiscountingPerpetualFuturesEngine
{
    /// <summary>Prices perpetual futures: QuantLib's <c>calculate</c>.</summary>
    /// <param name="futures">The perpetual futures.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="domesticDiscountCurve">The domestic currency's discount curve.</param>
    /// <param name="foreignDiscountCurve">The foreign currency's discount curve.</param>
    /// <param name="assetSpot">The exchange rate's spot.</param>
    /// <param name="fundingTimes">The funding rates' times.</param>
    /// <param name="fundingRates">The funding rates.</param>
    /// <param name="interestRateDiffs">The interest-rate differences.</param>
    /// <param name="fundingInterpType">How the rates are interpolated between their times.</param>
    /// <param name="maxT">The horizon past which every rate is flat and the tail is summed in closed form.</param>
    /// <returns>The results.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fundingInterpType"/> isn't a member of its
    /// type.</exception>
    /// <exception cref="ArgumentException">A list is empty, or the lists' sizes differ.</exception>
    /// <exception cref="NanoQLException">
    /// The payoff is a quanto one, the funding frequency's unit isn't a time unit, or the tail diverges.
    /// </exception>
    public static InstrumentResults Calculate(
        PerpetualFutures futures,
        EvaluationContext context,
        YieldTermStructure domesticDiscountCurve,
        YieldTermStructure foreignDiscountCurve,
        double assetSpot,
        IEnumerable<double> fundingTimes,
        IEnumerable<double> fundingRates,
        IEnumerable<double> interestRateDiffs,
        DiscountingPerpetualFuturesEngineInterpolationType fundingInterpType =
            DiscountingPerpetualFuturesEngineInterpolationType.PiecewiseConstant,
        double maxT = 60.0)
    {
        ArgumentNullException.ThrowIfNull(futures);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(domesticDiscountCurve);
        ArgumentNullException.ThrowIfNull(foreignDiscountCurve);
        ArgumentNullException.ThrowIfNull(fundingTimes);
        ArgumentNullException.ThrowIfNull(fundingRates);
        ArgumentNullException.ThrowIfNull(interestRateDiffs);
        EnumArguments.Check(fundingInterpType, nameof(fundingInterpType));
        ImmutableArray<double> times = [.. fundingTimes], rates = [.. fundingRates], diffs = [.. interestRateDiffs];
        if (times.IsEmpty)
        {
            throw new ArgumentException("fundingTimes is empty", nameof(fundingTimes));
        }

        if (rates.IsEmpty)
        {
            throw new ArgumentException("fundingRates is empty", nameof(fundingRates));
        }

        if (diffs.IsEmpty)
        {
            throw new ArgumentException("interestRateDiffs is empty", nameof(interestRateDiffs));
        }

        if (times.Length != rates.Length)
        {
            throw new ArgumentException("fundingTimes and fundingRates must have the same size.", nameof(fundingRates));
        }

        if (times.Length != diffs.Length)
        {
            throw new ArgumentException(
                "fundingTimes and interestRateDiffs must have the same size.", nameof(interestRateDiffs));
        }

        if (futures.IsExpired(context))
        {
            return futures.SetupExpired();
        }

        futures.Validate();
        double factor = Factor(
            futures, context, domesticDiscountCurve, foreignDiscountCurve, times, rates, diffs, fundingInterpType, maxT);
        double value = futures.PayoffType == PerpetualFuturesPayoffType.Linear ? assetSpot * factor : assetSpot / factor;
        return futures.FetchResults(new InstrumentResults { NPV = value });
    }

    // QuantLib's calculate() to the factor the spot multiplies or divides.
    private static double Factor(
        PerpetualFutures futures,
        EvaluationContext context,
        YieldTermStructure domesticDiscountCurve,
        YieldTermStructure foreignDiscountCurve,
        ImmutableArray<double> fundingTimes,
        ImmutableArray<double> fundingRates,
        ImmutableArray<double> interestRateDiffs,
        DiscountingPerpetualFuturesEngineInterpolationType fundingInterpType,
        double maxT)
    {
        if (!(futures.PayoffType == PerpetualFuturesPayoffType.Linear
            || futures.PayoffType == PerpetualFuturesPayoffType.Inverse))
        {
            throw new NanoQLException(
                "Only Linear and Inverse payoffs are supported in DiscountingPerpetualFuturesEngine");
        }

        // Linear payoff <--> Inverse payoff:
        // 1. exchange domestic and foreign curves
        // 2. future price: f <--> 1/f
        YieldTermStructure effDomCurve = futures.PayoffType == PerpetualFuturesPayoffType.Linear
            ? domesticDiscountCurve
            : foreignDiscountCurve;
        YieldTermStructure effForCurve = futures.PayoffType == PerpetualFuturesPayoffType.Linear
            ? foreignDiscountCurve
            : domesticDiscountCurve;

        Period fundingFreq = futures.FundingFrequency;
        Date refDate = context.EvaluationDate;
        DayCounter dc = futures.DayCounter;
        Calendar cal = futures.Calendar;

        // the grid's clock: the instrument's day counter from the evaluation date
        TermStructure clock = new FlatForward(refDate, 0.0, dc);

        Interpolation fundingRateInterp = SelectInterpolation(fundingInterpType, fundingTimes, fundingRates)
            .EnableExtrapolation();
        Interpolation interestRateDiffInterp = SelectInterpolation(fundingInterpType, fundingTimes, interestRateDiffs)
            .EnableExtrapolation();

        if (fundingFreq.Length > 0)
        {
            // discrete-time case
            var timeGrid = new List<double>();
            double tGrid = 0.0;
            while (tGrid < maxT)
            {
                timeGrid.Add(tGrid);
                double tUnit;
                Date date = YearFractionToDateFunctions.YearFractionToDate(dc, refDate, tGrid);
                double daysInYear = dc.DayCount(new Date(1, Month.January, date.Year), new Date(1, Month.January, date.Year + 1));
                switch (fundingFreq.Units)
                {
                    case TimeUnit.Years:
                        tGrid += fundingFreq.Length;
                        break;
                    case TimeUnit.Months:
                        tUnit = 1.0 / 12.0;
                        tGrid += tUnit * fundingFreq.Length;
                        break;
                    case TimeUnit.Weeks:
                    case TimeUnit.Days:
                        tGrid = dc.YearFraction(refDate, cal.Advance(date, fundingFreq));
                        break;
                    case TimeUnit.Hours:
                        tUnit = 1.0 / daysInYear / 24.0;
                        tGrid += tUnit * fundingFreq.Length;
                        break;
                    case TimeUnit.Minutes:
                        tUnit = 1.0 / daysInYear / 24.0 / 60.0;
                        tGrid += tUnit * fundingFreq.Length;
                        break;
                    case TimeUnit.Seconds:
                        tUnit = 1.0 / daysInYear / 24.0 / 60.0 / 60.0;
                        tGrid += tUnit * fundingFreq.Length;
                        break;
                    case TimeUnit.Milliseconds:
                        tUnit = 1.0 / daysInYear / 24.0 / 60.0 / 60.0 / 1000.0;
                        tGrid += tUnit * fundingFreq.Length;
                        break;
                    case TimeUnit.Microseconds:
                        tUnit = 1.0 / daysInYear / 24.0 / 60.0 / 60.0 / 1000.0 / 1000.0;
                        tGrid += tUnit * fundingFreq.Length;
                        break;
                    default:
                        throw new NanoQLException("Unknown unit in fundingFrequency");
                }
            }

            int n = timeGrid.Count;
            double[] fundingRateGrid = new double[n];
            double[] interestRateDiffGrid = new double[n];
            for (int i = 0; i < n; ++i)
            {
                double time = timeGrid[i];
                fundingRateGrid[i] = fundingRateInterp.Evaluate(time);
                interestRateDiffGrid[i] = interestRateDiffInterp.Evaluate(time);
            }

            if (futures.FundingType == PerpetualFuturesFundingType.FundingWithCurrentSpot)
            {
                double ratioFx = 1.0;
                int i;
                for (i = 0; i < n - 1; ++i)
                {
                    double time = timeGrid[i];
                    double nextTime = timeGrid[i + 1];
                    ratioFx = effForCurve.DiscountAt(clock, nextTime) / effForCurve.DiscountAt(clock, time)
                        / effDomCurve.DiscountAt(clock, nextTime) * effDomCurve.DiscountAt(clock, time);
                    fundingRateGrid[i] *= ratioFx;
                    interestRateDiffGrid[i] *= ratioFx;
                }

                // for i = timeGrid.size() - 1
                fundingRateGrid[i] *= ratioFx;
                interestRateDiffGrid[i] *= ratioFx;
            }

            int iLast = n - 1;
            double timeLast = timeGrid[iLast];
            double fundingRateGridLast = fundingRateGrid[iLast];
            double interestRateDiffGridLast = interestRateDiffGrid[iLast];
            double domRateLast = effDomCurve.ContinuousForwardRateAt(clock, timeLast, timeLast);
            double forRateLast = effForCurve.ContinuousForwardRateAt(clock, timeLast, timeLast);

            // for t > maxT_, assume flat extrapolation on all rates; the tail's geometric ratio below 1, 1 + k
            // positive, or the series diverges
            double timeStep = (timeGrid[^1] - timeGrid[0]) / (n - 1);
            if (!(1.0 + fundingRateGridLast > 0.0))
            {
                throw new NanoQLException(
                    "1 + fundingRate at max time is not positive. Because the last funding rate is flatly "
                    + "extrapolated, the sum diverges.");
            }

            double ratio = 1.0 / (1.0 + fundingRateGridLast) * Math.Exp(-timeStep * (forRateLast - domRateLast));
            if (!(ratio < 1.0))
            {
                throw new NanoQLException(
                    "(1 + fundingRate) exp(timeStep (foreign rate - domestic rate)) at max time is not above 1. "
                    + "Because the last rates are flatly extrapolated, the sum diverges.");
            }

            // the funding discounts' product, each period's division in QuantLib's order (ED20)
            double sum = 0.0;
            double product = 1.0;
            for (int i = 0; i < n - 1; ++i)
            {
                double time = timeGrid[i];
                product /= 1.0 + fundingRateGrid[i];
                sum += product * (fundingRateGrid[i] - interestRateDiffGrid[i])
                    * effForCurve.DiscountAt(clock, time) / effDomCurve.DiscountAt(clock, time);
            }

            double productIRDiffLast = product / (1.0 + fundingRateGrid[iLast]);
            double lastTerm = productIRDiffLast
                * (fundingRateGridLast - interestRateDiffGridLast)
                * effForCurve.DiscountAt(clock, timeLast) / effDomCurve.DiscountAt(clock, timeLast);
            sum += lastTerm / (1.0 - ratio);
            return sum;
        }
        else
        {
            // continuous-time case
            var integrator = new TrapezoidIntegral<Default>(1.0e-6, 30);
            double fundingRateXMax = fundingRateInterp.XMax;
            double ExpIRDiff(double s)
            {
                if (s < fundingRateXMax)
                {
                    return Math.Exp(-integrator.Integrate(x => fundingRateInterp.Evaluate(x), 0.0, s));
                }
                else
                {
                    return Math.Exp(-integrator.Integrate(x => fundingRateInterp.Evaluate(x), 0.0, fundingRateXMax)
                        - (fundingRateInterp.Evaluate(fundingRateXMax) * (s - fundingRateXMax)));
                }
            }

            // for t > maxT_, assume flat extrapolation on all rates; the tail's rate positive, or the integral diverges
            double fundingRateLast = fundingRateInterp.Evaluate(maxT);
            double interestRateDiffLast = interestRateDiffInterp.Evaluate(maxT);
            double domRateLast = effDomCurve.ContinuousForwardRateAt(clock, maxT, maxT);
            double forRateLast = effForCurve.ContinuousForwardRateAt(clock, maxT, maxT);
            double ratio = fundingRateLast + forRateLast - domRateLast;
            if (!(ratio > 0.0))
            {
                throw new NanoQLException(
                    "fundingRate + foreign rate - domestic rate at max time is not positive. Because the last rates "
                    + "are flatly extrapolated, integral diverges.");
            }

            double TimeIntegrand(double s) =>
                (fundingRateInterp.Evaluate(s) - interestRateDiffInterp.Evaluate(s)) * ExpIRDiff(s)
                * effForCurve.DiscountAt(clock, s) / effDomCurve.DiscountAt(clock, s);
            double factor = integrator.Integrate(TimeIntegrand, 0.0, maxT);

            double expIRDiffLast = ExpIRDiff(maxT);
            factor += (fundingRateLast - interestRateDiffLast) * expIRDiffLast
                * effForCurve.DiscountAt(clock, maxT) / effDomCurve.DiscountAt(clock, maxT) / ratio;
            return factor;
        }
    }

    private static Interpolation SelectInterpolation(
        DiscountingPerpetualFuturesEngineInterpolationType type, ImmutableArray<double> times, ImmutableArray<double> values) =>
        type switch
        {
            DiscountingPerpetualFuturesEngineInterpolationType.Linear => new LinearInterpolation(times.AsSpan(), values.AsSpan()),
            DiscountingPerpetualFuturesEngineInterpolationType.PiecewiseConstant =>
                new BackwardFlatInterpolation(times.AsSpan(), values.AsSpan()),
            _ => new CubicNaturalSpline(times.AsSpan(), values.AsSpan()),
        };
}
