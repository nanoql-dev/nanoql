// SPDX-FileCopyrightText: Copyright (C) 2008 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/asian/mc_discr_arith_av_strike.hpp ql/pricingengines/asian/mc_discr_arith_av_strike.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Instruments;
using NanoQL.Methods;

namespace NanoQL.PricingEngines;

/// <summary>
/// The path pricer of a discrete arithmetic average-strike Asian option: the discounted payoff of the path's last value
/// struck at the average of its fixings and the past ones.
/// </summary>
/// <remarks>
/// QuantLib's: the path's nodes fixings but its start, which is one where the grid's first time is 0, and, where a
/// fixing count is given, its last node the exercise after them. The engine's own route weighs each node by the number
/// of fixings on its time, the exercise node none, so a fixing date given twice counts twice, QuantLib's sum to the bit
/// where each time holds one fixing.
/// </remarks>
public sealed class ArithmeticASOPathPricer : PathPricer<MonteCarloPath>
{
    private readonly OptionType type;
    private readonly double discount;
    private readonly double runningSum;
    private readonly int pastFixings;
    private readonly int? fixingCount;
    private readonly ImmutableArray<int> multiplicities;

    /// <summary>Initializes a new instance of the <see cref="ArithmeticASOPathPricer"/> class.</summary>
    /// <param name="type">The option type.</param>
    /// <param name="discount">The discount factor to the exercise.</param>
    /// <param name="runningSum">The sum of the past fixings.</param>
    /// <param name="pastFixings">The number of past fixings.</param>
    /// <param name="fixingCount">The number of the path's nodes that are fixings, its start included, or
    /// <see langword="null"/> for every node.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type, or
    /// <paramref name="pastFixings"/> or <paramref name="fixingCount"/> is negative.</exception>
    public ArithmeticASOPathPricer(
        OptionType type, double discount, double runningSum = 0.0, int pastFixings = 0, int? fixingCount = null)
        : this(type, discount, runningSum, pastFixings, fixingCount, default)
    {
    }

    // the engine's route: the number of fixings at each node of the grid, the exercise node's 0
    internal ArithmeticASOPathPricer(
        OptionType type, double discount, double runningSum, int pastFixings, int? fixingCount, ImmutableArray<int> multiplicities)
    {
        EnumArguments.Check(type, nameof(type));
        ArgumentOutOfRangeException.ThrowIfNegative(pastFixings);
        if (fixingCount is int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count, nameof(fixingCount));
        }

        this.type = type;
        this.discount = discount;
        this.runningSum = runningSum;
        this.pastFixings = pastFixings;
        this.fixingCount = fixingCount;
        this.multiplicities = multiplicities;
    }

    /// <summary>Prices a path.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The discounted payoff.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The path has no node after its start, or the fixing count exceeds its length.</exception>
    public override double Evaluate(MonteCarloPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        int n = path.Length;
        if (!(n > 1))
        {
            throw new NanoQLException("the path cannot be empty");
        }

        // When fixingCount_ is set, the path may include an extra point
        // at the exercise date beyond the last fixing date.  Average
        // only over the fixing points; use path.back() for the spot
        // at exercise.
        int nFixings = fixingCount ?? n;
        if (!(nFixings <= n))
        {
            throw new NanoQLException(
                $"fixingCount ({nFixings.ToString(CultureInfo.InvariantCulture)}) exceeds path length ({n.ToString(CultureInfo.InvariantCulture)})");
        }

        // include initial fixing (T=0 is a fixing date), or skip the first path point, T=0, not a fixing
        int first = path.TimeGrid.MandatoryTimes[0] == 0.0 ? 0 : 1;
        double sum = runningSum;
        int fixings = pastFixings;
        if (multiplicities.IsDefault)
        {
            for (int i = first; i < nFixings; ++i)
            {
                sum += path[i];
            }

            fixings += nFixings - first;
        }
        else
        {
            for (int i = first; i < nFixings; ++i)
            {
                int m = multiplicities[i];
                if (m != 0)
                {
                    sum += m == 1 ? path[i] : m * path[i];
                    fixings += m;
                }
            }
        }

        double averageStrike = sum / fixings;
        return discount * PlainVanillaPayoff.Value(type, averageStrike, path.Back());
    }
}
