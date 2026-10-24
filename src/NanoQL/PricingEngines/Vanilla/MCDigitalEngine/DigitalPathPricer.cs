// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2003 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/mcdigitalengine.hpp ql/pricingengines/vanilla/mcdigitalengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.PricingEngines;

/// <summary>
/// The path pricer of an American cash-or-nothing option paid at the hit or at expiry: the strike's crossing within
/// each step is detected by the Brownian bridge's maximum (or minimum), drawn from a uniform sequence of its own.
/// </summary>
/// <remarks>
/// <para>
/// A working type (S2R2), as in QuantLib: it draws a new uniform sequence for each path it prices, from a copy of the
/// generator it is given. References: Beaglehole, Dybvig and Zhou, "Going to Extremes: Correcting Simulation Bias in
/// Exotic Option Valuation", Financial Analysts Journal, Jan/Feb 1997; El Babsiri and Noel, "Simulating
/// path-dependent options: A new approach", Journal of Derivatives, Winter 1998.
/// </para>
/// <para>
/// Two documented divergences. A step's crossing probability reads the volatility at the step's start, (t_i, S_i),
/// where QuantLib reads it at the step's end time, <c>diffusion(timeGrid[i+1], S_i)</c>, whose comment calls it the
/// initial one (the S-van plan's VR4 (iv), <c>Divergences/McDigitalStepVolatilityTests.cs</c>): the same on a
/// volatility constant in time. And crossings count from the exercise window's opening, a path already past the strike
/// there paid at it, where QuantLib counts them from the start (VR4 (iii), <c>Divergences/McDigitalWindowTests.cs</c>);
/// a window open from the start is QuantLib's.
/// </para>
/// </remarks>
public sealed class DigitalPathPricer : PathPricer<MonteCarloPath>
{
    private readonly CashOrNothingPayoff payoff;
    private readonly AmericanExercise exercise;
    private readonly StochasticProcess1D diffProcess;
    private readonly MersenneTwisterUniformRsg sequenceGen;
    private readonly YieldTermStructure discountTS;
    private readonly TermStructure clock;
    private readonly double windowStart;

    /// <summary>Initializes a new instance of the <see cref="DigitalPathPricer"/> class.</summary>
    /// <param name="payoff">The cash-or-nothing payoff.</param>
    /// <param name="exercise">The American exercise.</param>
    /// <param name="discountTS">The discount curve, whose times are the paths'.</param>
    /// <param name="diffProcess">The process, whose diffusion the bridge reads and whose times are the paths'.</param>
    /// <param name="sequenceGen">The uniform sequence generator, of the grid's steps, which the pricer copies.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public DigitalPathPricer(
        CashOrNothingPayoff payoff,
        AmericanExercise exercise,
        YieldTermStructure discountTS,
        StochasticProcess1D diffProcess,
        MersenneTwisterUniformRsg sequenceGen)
        : this(payoff, exercise, discountTS, discountTS, diffProcess, sequenceGen)
    {
    }

    // The engine's form: the curve read at the instants the paths' times denote on another clock (the S-van plan's
    // VR1 (v)).
    internal DigitalPathPricer(
        CashOrNothingPayoff payoff,
        AmericanExercise exercise,
        YieldTermStructure discountTS,
        TermStructure clock,
        StochasticProcess1D diffProcess,
        MersenneTwisterUniformRsg sequenceGen)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        ArgumentNullException.ThrowIfNull(exercise);
        ArgumentNullException.ThrowIfNull(discountTS);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(diffProcess);
        ArgumentNullException.ThrowIfNull(sequenceGen);
        this.payoff = payoff;
        this.exercise = exercise;
        this.diffProcess = diffProcess;
        this.sequenceGen = sequenceGen.Clone();
        this.discountTS = discountTS;
        this.clock = clock;
        windowStart = diffProcess.Time(exercise.Dates[0]);
    }

    /// <summary>The discounted cash where the path crosses the strike, at the crossing step's end or at expiry; else 0.</summary>
    /// <param name="path">The path, on a grid holding the exercise window's opening where it opens after the start.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The path has fewer than two points, or the window opens after the start at a time its grid doesn't hold.
    /// </exception>
    public override double Evaluate(MonteCarloPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        int n = path.Length;
        if (!(n > 1))
        {
            throw new NanoQLException("the path cannot be empty");
        }

        double logAssetPrice = Math.Log(path.Front());
        double x, y;
        double vol;
        NanoQL.TimeGrid timeGrid = path.TimeGrid;
        double dt;
        ReadOnlySpan<double> u = sequenceGen.NextSequence().Value.Span;
        double logStrike = Math.Log(payoff.Strike);

        // the window's opening on the grid (the S-van plan's VR4 (iii)); QuantLib counts from the start
        int start = windowStart > 0.0 ? timeGrid.Index(windowStart) : 0;
        for (int j = 0; j < start; j++)
        {
            logAssetPrice += Math.Log(path[j + 1] / path[j]);
        }

        int i;
        switch (payoff.OptionType)
        {
            case OptionType.Call:
                if (start > 0 && logAssetPrice >= logStrike)
                {
                    // already past the strike when the window opens: paid then
                    return Paid(timeGrid, start);
                }

                for (i = start; i < n - 1; i++)
                {
                    x = Math.Log(path[i + 1] / path[i]);

                    // the volatility at the step's start (the S-van plan's VR4 (iv)), where QuantLib reads
                    // diffProcess_->diffusion(timeGrid[i+1], ...)
                    vol = diffProcess.Diffusion(timeGrid[i], Math.Exp(logAssetPrice));
                    dt = timeGrid.Dt(i);
                    y = logAssetPrice + (0.5 * (x + Math.Sqrt((x * x) - (2 * vol * vol * dt * Math.Log(1 - u[i])))));

                    // cross the strike
                    if (y >= logStrike)
                    {
                        return Paid(timeGrid, i + 1);
                    }

                    logAssetPrice += x;
                }

                break;
            case OptionType.Put:
                if (start > 0 && logAssetPrice <= logStrike)
                {
                    // already past the strike when the window opens: paid then
                    return Paid(timeGrid, start);
                }

                for (i = start; i < n - 1; i++)
                {
                    x = Math.Log(path[i + 1] / path[i]);

                    // the volatility at the step's start (the S-van plan's VR4 (iv)), where QuantLib reads
                    // diffProcess_->diffusion(timeGrid[i+1], ...)
                    vol = diffProcess.Diffusion(timeGrid[i], Math.Exp(logAssetPrice));
                    dt = timeGrid.Dt(i);
                    y = logAssetPrice + (0.5 * (x - Math.Sqrt((x * x) - (2 * vol * vol * dt * Math.Log(u[i])))));
                    if (y <= logStrike)
                    {
                        return Paid(timeGrid, i + 1);
                    }

                    logAssetPrice += x;
                }

                break;
            default:
                throw new NanoQLException("unknown option type");
        }

        return 0.0;
    }

    // The cash, discounted from expiry where it is paid then, otherwise from the grid's i-th time: QuantLib's comment,
    // "the discount should be calculated at the exercise time between path.timeGrid()[i+1] and path.timeGrid()[i+2]".
    private double Paid(NanoQL.TimeGrid timeGrid, int i) => exercise.PayoffAtExpiry
        ? payoff.CashPayoff * discountTS.DiscountAt(clock, timeGrid.Back())
        : payoff.CashPayoff * discountTS.DiscountAt(clock, timeGrid[i]);
}
