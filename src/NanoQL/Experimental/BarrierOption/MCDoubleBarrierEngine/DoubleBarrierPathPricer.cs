// SPDX-FileCopyrightText: Copyright (C) 2020 Lew Wei Hao
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/barrieroption/mcdoublebarrierengine.hpp ql/experimental/barrieroption/mcdoublebarrierengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Methods;

namespace NanoQL.Experimental;

/// <summary>
/// The path pricer of a double barrier option: either barrier is checked at the path's points, a knock-out paying its
/// rebate at the first hit's node and a knock-in its rebate at expiry where neither is hit.
/// </summary>
/// <remarks>Immutable and public, as QuantLib's (the S-xeq plan's XED8).</remarks>
[Experimental("NQL0001")]
public sealed class DoubleBarrierPathPricer : PathPricer<MonteCarloPath>
{
    private readonly DoubleBarrierType barrierType;
    private readonly double barrierLow;
    private readonly double barrierHigh;
    private readonly double rebate;
    private readonly OptionType type;
    private readonly double strike;
    private readonly ImmutableArray<double> discounts;

    /// <summary>Initializes a new instance of the <see cref="DoubleBarrierPathPricer"/> class.</summary>
    /// <param name="barrierType">The barrier's type, a knock-in or a knock-out.</param>
    /// <param name="barrierLow">The lower barrier, positive.</param>
    /// <param name="barrierHigh">The upper barrier, positive.</param>
    /// <param name="rebate">The rebate.</param>
    /// <param name="type">The option type.</param>
    /// <param name="strike">The strike, non-negative.</param>
    /// <param name="discounts">The discount factors at the path's grid times.</param>
    /// <exception cref="ArgumentNullException"><paramref name="discounts"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="barrierType"/> or <paramref name="type"/> isn't a
    /// member of its type.</exception>
    /// <exception cref="ArgumentException">The strike is negative ("strike less than zero not allowed") or a barrier
    /// isn't positive.</exception>
    public DoubleBarrierPathPricer(
        DoubleBarrierType barrierType,
        double barrierLow,
        double barrierHigh,
        double rebate,
        OptionType type,
        double strike,
        IEnumerable<double> discounts)
    {
        ArgumentNullException.ThrowIfNull(discounts);
        EnumArguments.Check(barrierType, nameof(barrierType));
        EnumArguments.Check(type, nameof(type));
        if (!(strike >= 0.0))
        {
            throw new ArgumentException("strike less than zero not allowed", nameof(strike));
        }

        if (!(barrierLow > 0.0))
        {
            throw new ArgumentException("low barrier less/equal zero not allowed", nameof(barrierLow));
        }

        if (!(barrierHigh > 0.0))
        {
            throw new ArgumentException("high barrier less/equal zero not allowed", nameof(barrierHigh));
        }

        this.barrierType = barrierType;
        this.barrierLow = barrierLow;
        this.barrierHigh = barrierHigh;
        this.rebate = rebate;
        this.type = type;
        this.strike = strike;
        this.discounts = [.. discounts];
    }

    /// <summary>The discounted payoff on a path, the barriers checked at its points.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The path has fewer than two points, or the type is a KIKO or a KOKI
    /// ("unknown barrier type").</exception>
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
        double terminalPrice = path.Back();

        switch (barrierType)
        {
            case DoubleBarrierType.KnockOut:
                isOptionActive = true;
                for (int i = 0; i < n - 1; i++)
                {
                    double newAssetPrice = path[i + 1];
                    if (newAssetPrice >= barrierHigh || newAssetPrice <= barrierLow)
                    {
                        isOptionActive = false;
                        knockNode ??= i + 1;
                        break;
                    }
                }

                break;
            case DoubleBarrierType.KnockIn:
                isOptionActive = false;
                for (int i = 0; i < n - 1; i++)
                {
                    double newAssetPrice = path[i + 1];
                    if (newAssetPrice >= barrierHigh || newAssetPrice <= barrierLow)
                    {
                        isOptionActive = true;
                        knockNode ??= i + 1;
                        break;
                    }
                }

                break;
            default:
                throw new NanoQLException("unknown barrier type");
        }

        if (isOptionActive)
        {
            return PlainVanillaPayoff.Value(type, strike, terminalPrice) * discounts[^1];
        }

        return barrierType == DoubleBarrierType.KnockOut ? rebate * discounts[knockNode!.Value] : rebate * discounts[^1];
    }
}
