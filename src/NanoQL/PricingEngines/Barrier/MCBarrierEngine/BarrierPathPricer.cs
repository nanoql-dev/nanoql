// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2003 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/barrier/mcbarrierengine.hpp ql/pricingengines/barrier/mcbarrierengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.PricingEngines;

/// <summary>
/// The path pricer of a barrier option with the Brownian bridge correction for the barrier's crossings between the
/// path's points: each step's extreme is drawn from its bridge, given a uniform number of its own (Beaglehole, Dybvig
/// and Zhou, "Going to Extremes: Correcting Simulation Bias in Exotic Option Valuation", Financial Analysts Journal,
/// Jan/Feb 1997; El Babsiri and Noel, "Simulating path-dependent options: A new approach", Journal of Derivatives,
/// Winter 1998).
/// </summary>
/// <remarks>
/// A working type, as in QuantLib: it draws a new uniform sequence for each path it prices, from a copy of the
/// generator it is given, read as a view of the generator's buffer where QuantLib copies a vector per path, so a price
/// allocates nothing. A knock-out pays its rebate at the hit's node, a knock-in at expiry.
/// </remarks>
[WorkingType]
public sealed class BarrierPathPricer : PathPricer<MonteCarloPath>
{
    private readonly BarrierType barrierType;
    private readonly double barrier;
    private readonly double rebate;
    private readonly StochasticProcess1D diffProcess;
    private readonly MersenneTwisterUniformRsg sequenceGen;
    private readonly OptionType type;
    private readonly double strike;
    private readonly ImmutableArray<double> discounts;

    /// <summary>Initializes a new instance of the <see cref="BarrierPathPricer"/> class.</summary>
    /// <param name="barrierType">The barrier's type.</param>
    /// <param name="barrier">The barrier, positive.</param>
    /// <param name="rebate">The rebate.</param>
    /// <param name="type">The option type.</param>
    /// <param name="strike">The strike, non-negative.</param>
    /// <param name="discounts">The discount factors at the path's grid times.</param>
    /// <param name="diffProcess">The process, whose diffusion the bridge reads at each step's start.</param>
    /// <param name="sequenceGen">The uniform sequence generator, of the grid's steps, which the pricer copies.</param>
    /// <exception cref="ArgumentNullException">A reference argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="barrierType"/> or <paramref name="type"/> isn't a
    /// member of its type.</exception>
    /// <exception cref="ArgumentException">The strike is negative ("strike less than zero not allowed") or the barrier
    /// isn't positive ("barrier less/equal zero not allowed").</exception>
    public BarrierPathPricer(
        BarrierType barrierType,
        double barrier,
        double rebate,
        OptionType type,
        double strike,
        IEnumerable<double> discounts,
        StochasticProcess1D diffProcess,
        MersenneTwisterUniformRsg sequenceGen)
    {
        ArgumentNullException.ThrowIfNull(discounts);
        ArgumentNullException.ThrowIfNull(diffProcess);
        ArgumentNullException.ThrowIfNull(sequenceGen);
        EnumArguments.Check(barrierType, nameof(barrierType));
        EnumArguments.Check(type, nameof(type));
        if (!(strike >= 0.0))
        {
            throw new ArgumentException("strike less than zero not allowed", nameof(strike));
        }

        if (!(barrier > 0.0))
        {
            throw new ArgumentException("barrier less/equal zero not allowed", nameof(barrier));
        }

        this.barrierType = barrierType;
        this.barrier = barrier;
        this.rebate = rebate;
        this.diffProcess = diffProcess;
        this.sequenceGen = sequenceGen.Clone();
        this.type = type;
        this.strike = strike;
        this.discounts = [.. discounts];
    }

    /// <summary>The discounted payoff on a path, its crossings of the barrier between points drawn from the bridge.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The path has fewer than two points.</exception>
    public override double Evaluate(MonteCarloPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        int n = path.Length;
        if (!(n > 1))
        {
            throw new NanoQLException("the path cannot be empty");
        }

        bool isOptionActive;
        int? knockNode = null;
        double assetPrice = path.Front();
        double newAssetPrice;
        double x, y;
        double vol;
        NanoQL.TimeGrid timeGrid = path.TimeGrid;
        double dt;
        ReadOnlySpan<double> u = sequenceGen.NextSequence().Value.Span;
        int i;

        switch (barrierType)
        {
            case BarrierType.DownIn:
                isOptionActive = false;
                for (i = 0; i < n - 1; i++)
                {
                    newAssetPrice = path[i + 1];

                    // terminal or initial vol?
                    vol = diffProcess.Diffusion(timeGrid[i], assetPrice);
                    dt = timeGrid.Dt(i);

                    x = Math.Log(newAssetPrice / assetPrice);
                    y = 0.5 * (x - Math.Sqrt((x * x) - (2 * vol * vol * dt * Math.Log(u[i]))));
                    y = assetPrice * Math.Exp(y);
                    if (y <= barrier)
                    {
                        isOptionActive = true;
                        knockNode ??= i + 1;
                    }

                    assetPrice = newAssetPrice;
                }

                break;
            case BarrierType.UpIn:
                isOptionActive = false;
                for (i = 0; i < n - 1; i++)
                {
                    newAssetPrice = path[i + 1];

                    // terminal or initial vol?
                    vol = diffProcess.Diffusion(timeGrid[i], assetPrice);
                    dt = timeGrid.Dt(i);

                    x = Math.Log(newAssetPrice / assetPrice);
                    y = 0.5 * (x + Math.Sqrt((x * x) - (2 * vol * vol * dt * Math.Log(1 - u[i]))));
                    y = assetPrice * Math.Exp(y);
                    if (y >= barrier)
                    {
                        isOptionActive = true;
                        knockNode ??= i + 1;
                    }

                    assetPrice = newAssetPrice;
                }

                break;
            case BarrierType.DownOut:
                isOptionActive = true;
                for (i = 0; i < n - 1; i++)
                {
                    newAssetPrice = path[i + 1];

                    // terminal or initial vol?
                    vol = diffProcess.Diffusion(timeGrid[i], assetPrice);
                    dt = timeGrid.Dt(i);

                    x = Math.Log(newAssetPrice / assetPrice);
                    y = 0.5 * (x - Math.Sqrt((x * x) - (2 * vol * vol * dt * Math.Log(u[i]))));
                    y = assetPrice * Math.Exp(y);
                    if (y <= barrier)
                    {
                        isOptionActive = false;
                        knockNode ??= i + 1;
                    }

                    assetPrice = newAssetPrice;
                }

                break;
            case BarrierType.UpOut:
                isOptionActive = true;
                for (i = 0; i < n - 1; i++)
                {
                    newAssetPrice = path[i + 1];

                    // terminal or initial vol?
                    vol = diffProcess.Diffusion(timeGrid[i], assetPrice);
                    dt = timeGrid.Dt(i);

                    x = Math.Log(newAssetPrice / assetPrice);
                    y = 0.5 * (x + Math.Sqrt((x * x) - (2 * vol * vol * dt * Math.Log(1 - u[i]))));
                    y = assetPrice * Math.Exp(y);
                    if (y >= barrier)
                    {
                        isOptionActive = false;
                        knockNode ??= i + 1;
                    }

                    assetPrice = newAssetPrice;
                }

                break;
            default:
                throw new NanoQLException("unknown barrier type");
        }

        if (isOptionActive)
        {
            return PlainVanillaPayoff.Value(type, strike, assetPrice) * discounts[^1];
        }

        return barrierType switch
        {
            BarrierType.UpIn or BarrierType.DownIn => rebate * discounts[^1],
            _ => rebate * discounts[knockNode!.Value],
        };
    }
}
