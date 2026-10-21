// SPDX-FileCopyrightText: Copyright (C) 2009 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/meshers/fdmblackscholesmesher.hpp ql/methods/finitedifferences/meshers/fdmblackscholesmesher.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Methods;

/// <summary>
/// The one-dimensional mesher of a Black-Scholes process in the logarithm of the underlying: from below the lowest to
/// above the highest forward the dividends and the curves give up to maturity, by a multiple of the standard
/// deviation, concentrating around a critical point when one is given inside.
/// </summary>
/// <remarks>
/// Immutable. QuantLib reads the dividend curve and the volatility at the risk-free curve's times
/// (<c>fdmblackscholesmesher.cpp:63-103</c>); here each is read at the instants those times denote, on its own clock,
/// as <see cref="FdmBlackScholesOp"/> reads them, on the engine's times from the earliest reference date of the
/// structures, each accruing nothing before its own: the curves' discounts there, and the variance accrued to maturity.
/// A structure that shares the risk-free curve's day counter and reference date is read as QuantLib reads it; a
/// documented divergence (the S-eq plan's rulings of 2026-10-20 and 2026-10-21, under R8).
/// </remarks>
public sealed class FdmBlackScholesMesher : Fdm1dMesher
{
    /// <summary>Initializes a new instance of the <see cref="FdmBlackScholesMesher"/> class.</summary>
    /// <param name="size">The number of points, at least 2.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="maturity">The maturity, as a time on the engine's clock
    /// (<see cref="FdmBlackScholesOp.Clock"/>).</param>
    /// <param name="strike">The strike the volatility is read at.</param>
    /// <param name="xMinConstraint">The lower edge in log space, or <see langword="null"/> to compute it.</param>
    /// <param name="xMaxConstraint">The upper edge in log space, or <see langword="null"/> to compute it.</param>
    /// <param name="eps">The probability left outside each edge.</param>
    /// <param name="scaleFactor">The factor widening the edges.</param>
    /// <param name="cPoint">
    /// The critical point, in the underlying's units, and its density; <see langword="null"/> for none, QuantLib's
    /// <c>Null&lt;Real&gt;()</c>.
    /// </param>
    /// <param name="dividendSchedule">The cash dividends, empty by default, placed at their times on the engine's
    /// clock.</param>
    /// <param name="fdmQuantoHelper">The quanto helper, or <see langword="null"/> for none.</param>
    /// <param name="spotAdjustment">The adjustment added to the spot.</param>
    /// <param name="context">The evaluation context the dividends' amounts are read in; needed with dividends.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The spot isn't positive, a dividend is <see langword="null"/>, dividends are given without a context, or the
    /// size is below 2.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// A curve or the volatility can't be read, as at an instant before its reference date.
    /// </exception>
    public FdmBlackScholesMesher(
        int size,
        GeneralizedBlackScholesProcess process,
        double maturity,
        double strike,
        double? xMinConstraint = null,
        double? xMaxConstraint = null,
        double eps = 0.0001,
        double scaleFactor = 1.5,
        (double? Point, double? Density) cPoint = default,
        ImmutableArray<Dividend> dividendSchedule = default,
        FdmQuantoHelper? fdmQuantoHelper = null,
        double spotAdjustment = 0.0,
        EvaluationContext? context = null)
        : base(size)
    {
        ArgumentNullException.ThrowIfNull(process);
        ImmutableArray<Dividend> dividends = dividendSchedule.IsDefault ? [] : dividendSchedule;
        if (dividends.Any(d => d is null))
        {
            throw new ArgumentException("null dividend in schedule", nameof(dividendSchedule));
        }

        if (!dividends.IsEmpty && context is null)
        {
            throw new ArgumentException("an evaluation context is needed to read the dividends' amounts", nameof(context));
        }

        double s = process.X0;
        if (!(s > 0.0))
        {
            throw new ArgumentException("negative or null underlying given", nameof(process));
        }

        // the engine's times (FdmBlackScholesOp.Clock): the process's where no structure is dated before the risk-free
        // curve
        TermStructure clock = FdmBlackScholesOp.Clock(process, fdmQuantoHelper);
        var intermediateSteps = new List<(double Time, double Amount)>();
        foreach (Dividend d in dividends)
        {
            double t = clock.TimeFromReference(d.Date);
            if (t <= maturity && t >= 0.0)
            {
                intermediateSteps.Add((clock.TimeFromReference(d.Date), d.Amount(context!)));
            }
        }

        int intermediateTimeSteps = Math.Max(2, (int)(24.0 * maturity));
        for (int i = 0; i < intermediateTimeSteps; ++i)
        {
            intermediateSteps.Add(((i + 1) * (maturity / intermediateTimeSteps), 0.0));
        }

        // std::sort on (time, amount) pairs
        intermediateSteps.Sort(static (p, q) =>
            p.Time < q.Time ? -1 : q.Time < p.Time ? 1 : p.Amount < q.Amount ? -1 : q.Amount < p.Amount ? 1 : 0);

        YieldTermStructure rTS = process.RiskFreeRate;

        YieldTermStructure qTS = fdmQuantoHelper is not null
            ? new QuantoTermStructure(
                process.DividendYield,
                process.RiskFreeRate,
                fdmQuantoHelper.FTS,
                process.BlackVolatility,
                strike,
                fdmQuantoHelper.FxVolTS,
                fdmQuantoHelper.ExchRateATMlevel,
                fdmQuantoHelper.EquityFxCorrelation)
            : process.DividendYield;

        double lastDivTime = 0.0;
        double fwd = s + spotAdjustment;
        double mi = fwd, ma = fwd;

        foreach ((double divTime, double divAmount) in intermediateSteps)
        {
            fwd = fwd / rTS.DiscountAt(clock, divTime) * rTS.DiscountAt(clock, lastDivTime)
                * qTS.DiscountAt(clock, divTime) / qTS.DiscountAt(clock, lastDivTime);

            mi = Min(mi, fwd);
            ma = Max(ma, fwd);

            fwd -= divAmount;

            mi = Min(mi, fwd);
            ma = Max(ma, fwd);

            lastDivTime = divTime;
        }

        // Set the grid boundaries
        double normInvEps = new InverseCumulativeNormal().Evaluate(1 - eps);
        BlackVolTermStructure volTS = process.BlackVolatility;
        double sigmaSqrtT = volTS.SharesTimes(clock)
            ? volTS.BlackVol(maturity, strike) * Math.Sqrt(maturity)
            : Math.Sqrt(volTS.BlackForwardVarianceAt(clock, 0.0, maturity, strike));

        double xMin = Math.Log(mi) - (sigmaSqrtT * normInvEps * scaleFactor);
        double xMax = Math.Log(ma) + (sigmaSqrtT * normInvEps * scaleFactor);

        if (xMinConstraint is double xMinGiven)
        {
            xMin = xMinGiven;
        }

        if (xMaxConstraint is double xMaxGiven)
        {
            xMax = xMaxGiven;
        }

        Fdm1dMesher helper;
        if (cPoint.Point is double c
            && Math.Log(c) >= xMin && Math.Log(c) <= xMax)
        {
            helper = new Concentrating1dMesher(xMin, xMax, size, (Math.Log(c), cPoint.Density));
        }
        else
        {
            helper = new Uniform1dMesher(xMin, xMax, size);
        }

        for (int i = 0; i < size; ++i)
        {
            locations[i] = helper.Location(i);
            dplus[i] = helper.Dplus(i);
            dminus[i] = helper.Dminus(i);
        }
    }

    /// <summary>
    /// A Black-Scholes process on a constant volatility, dated by the risk-free curve: QuantLib's
    /// <c>processHelper</c>.
    /// </summary>
    /// <param name="s0">The underlying's value.</param>
    /// <param name="rTS">The risk-free curve.</param>
    /// <param name="qTS">The dividend curve.</param>
    /// <param name="vol">The volatility.</param>
    /// <returns>The process, its volatility on the risk-free curve's reference date and day counter, with no
    /// calendar.</returns>
    /// <exception cref="ArgumentNullException">A curve is <see langword="null"/>.</exception>
    public static GeneralizedBlackScholesProcess ProcessHelper(
        double s0, YieldTermStructure rTS, YieldTermStructure qTS, double vol)
    {
        ArgumentNullException.ThrowIfNull(rTS);
        ArgumentNullException.ThrowIfNull(qTS);
        return new GeneralizedBlackScholesProcess(
            s0,
            qTS,
            rTS,
            new BlackConstantVol(rTS.ReferenceDate, null, vol, rTS.DayCounter));
    }

    private static double Min(double a, double b) => b < a ? b : a;

    private static double Max(double a, double b) => a < b ? b : a;
}
