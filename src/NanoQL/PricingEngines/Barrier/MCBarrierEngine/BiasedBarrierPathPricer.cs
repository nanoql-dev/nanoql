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

namespace NanoQL.PricingEngines;

/// <summary>
/// The biased path pricer of a barrier option: the barrier is checked at the path's points only, so crossings between
/// them are missed.
/// </summary>
/// <remarks>Immutable. A knock-out pays its rebate at the hit's node, a knock-in at expiry.</remarks>
public sealed class BiasedBarrierPathPricer : PathPricer<MonteCarloPath>
{
    private readonly BarrierType barrierType;
    private readonly double barrier;
    private readonly double rebate;
    private readonly OptionType type;
    private readonly double strike;
    private readonly ImmutableArray<double> discounts;

    /// <summary>Initializes a new instance of the <see cref="BiasedBarrierPathPricer"/> class.</summary>
    /// <param name="barrierType">The barrier's type.</param>
    /// <param name="barrier">The barrier, positive.</param>
    /// <param name="rebate">The rebate.</param>
    /// <param name="type">The option type.</param>
    /// <param name="strike">The strike, non-negative.</param>
    /// <param name="discounts">The discount factors at the path's grid times.</param>
    /// <exception cref="ArgumentNullException"><paramref name="discounts"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="barrierType"/> or <paramref name="type"/> isn't a
    /// member of its type.</exception>
    /// <exception cref="ArgumentException">The strike is negative ("strike less than zero not allowed") or the barrier
    /// isn't positive ("barrier less/equal zero not allowed").</exception>
    public BiasedBarrierPathPricer(
        BarrierType barrierType, double barrier, double rebate, OptionType type, double strike, IEnumerable<double> discounts)
    {
        ArgumentNullException.ThrowIfNull(discounts);
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
        this.type = type;
        this.strike = strike;
        this.discounts = [.. discounts];
    }

    /// <summary>The discounted payoff on a path, the barrier checked at its points.</summary>
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
        int i;

        switch (barrierType)
        {
            case BarrierType.DownIn:
                isOptionActive = false;
                for (i = 1; i < n; i++)
                {
                    assetPrice = path[i];
                    if (assetPrice <= barrier)
                    {
                        isOptionActive = true;
                        knockNode ??= i;
                    }
                }

                break;
            case BarrierType.UpIn:
                isOptionActive = false;
                for (i = 1; i < n; i++)
                {
                    assetPrice = path[i];
                    if (assetPrice >= barrier)
                    {
                        isOptionActive = true;
                        knockNode ??= i;
                    }
                }

                break;
            case BarrierType.DownOut:
                isOptionActive = true;
                for (i = 1; i < n; i++)
                {
                    assetPrice = path[i];
                    if (assetPrice <= barrier)
                    {
                        isOptionActive = false;
                        knockNode ??= i;
                    }
                }

                break;
            case BarrierType.UpOut:
                isOptionActive = true;
                for (i = 1; i < n; i++)
                {
                    assetPrice = path[i];
                    if (assetPrice >= barrier)
                    {
                        isOptionActive = false;
                        knockNode ??= i;
                    }
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
