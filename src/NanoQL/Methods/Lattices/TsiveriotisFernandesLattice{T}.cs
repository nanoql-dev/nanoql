// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 Theo Boafo
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/lattices/tflattice.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Numerics;
using NanoQL.PricingEngines;

namespace NanoQL.Methods;

/// <summary>
/// The binomial lattice of the Tsiveriotis-Fernandes model: a Black-Scholes lattice whose step back carries each node's
/// conversion probability and discounts each branch at its node's blended rate, the risk-free rate and the risk-free
/// rate plus the credit spread weighted by that probability.
/// </summary>
/// <typeparam name="T">The binomial tree.</typeparam>
/// <remarks>
/// <para>
/// It rolls back a <see cref="DiscretizedConvertible"/> only, as QuantLib's does; another asset is refused. Its step
/// back is QuantLib's, the tree's first-node probabilities applied to every node:
/// <c>pd·v[j]/(1 + r[j]·dt) + pu·v[j + 1]/(1 + r[j + 1]·dt)</c>, the probabilities and rates of the step stepped back
/// likewise, into the asset's spare buffers (the S-sr plan's SSD9).
/// </para>
/// <para>
/// The volatility and the dividend yield, which QuantLib's constructor takes and doesn't read, are kept in the
/// signature.
/// </para>
/// </remarks>
public sealed class TsiveriotisFernandesLattice<T> : BlackScholesLattice<T>
    where T : Tree
{
    /// <summary>Initializes a new instance of the <see cref="TsiveriotisFernandesLattice{T}"/> class.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="riskFreeRate">The flat, continuously compounded risk-free rate.</param>
    /// <param name="end">The grid's last time.</param>
    /// <param name="steps">The grid's number of steps.</param>
    /// <param name="creditSpread">The credit spread.</param>
    /// <param name="volatility">The volatility, not read.</param>
    /// <param name="divYield">The dividend yield, not read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tree"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="steps"/> is negative.</exception>
    /// <exception cref="NanoQLException">The tree's up probability is above 1 or negative.</exception>
    public TsiveriotisFernandesLattice(
        T tree, double riskFreeRate, double end, int steps, double creditSpread, double volatility, double divYield)
        : base(tree, riskFreeRate, end, steps)
    {
        _ = volatility;
        _ = divYield;
        CreditSpread = creditSpread;
        if (!(pu <= 1.0))
        {
            throw new NanoQLException($"probability ({Show(pu)}) higher than one");
        }

        if (!(pu >= 0.0))
        {
            throw new NanoQLException($"negative ({Show(pu)}) probability");
        }
    }

    /// <summary>Gets the credit spread.</summary>
    public double CreditSpread { get; }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The asset isn't a <see cref="DiscretizedConvertible"/>.</exception>
    public override void Rollback(DiscretizedAsset asset, double to)
    {
        PartialRollback(asset, to);
        asset.AdjustValues();
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The asset isn't a <see cref="DiscretizedConvertible"/>.</exception>
    public override void PartialRollback(DiscretizedAsset asset, double to)
    {
        ArgumentNullException.ThrowIfNull(asset);
        double from = asset.Time;

        if (ComparisonFunctions.Close(from, to))
        {
            return;
        }

        if (!(from > to))
        {
            throw new NanoQLException($"cannot roll the asset back to{Show(to)} (it is already at t = {Show(from)})");
        }

        // QuantLib's dynamic_cast to a reference, which throws std::bad_cast
        if (asset is not DiscretizedConvertible convertible)
        {
            throw new ArgumentException(
                $"the Tsiveriotis-Fernandes lattice rolls back a convertible only, not a {asset.GetType().Name}", nameof(asset));
        }

        int iFrom = TimeGrid.Index(from);
        int iTo = TimeGrid.Index(to);

        for (int i = iFrom - 1; i >= iTo; --i)
        {
            int size = Size(i);
            Span<double> newValues = convertible.StepValues(size);
            convertible.StepArrays(size, out Span<double> newConversionProbability, out Span<double> newSpreadAdjustedRate);
            Stepback(
                i,
                convertible.Values,
                convertible.ConversionProbability,
                convertible.SpreadAdjustedRate,
                newValues,
                newConversionProbability,
                newSpreadAdjustedRate);
            convertible.CommitStep(size, TimeGrid[i]);
            convertible.CommitArrays();

            // skip the very last adjustment
            if (i != iTo)
            {
                convertible.AdjustValues();
            }
        }
    }

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);

    private void Stepback(
        int i,
        ReadOnlySpan<double> values,
        ReadOnlySpan<double> conversionProbability,
        ReadOnlySpan<double> spreadAdjustedRate,
        Span<double> newValues,
        Span<double> newConversionProbability,
        Span<double> newSpreadAdjustedRate)
    {
        double riskFreeRate = RiskFreeRate;
        double dt = Dt;
        int size = Size(i);
        for (int j = 0; j < size; j++)
        {
            // new conversion probability is calculated via backward
            // induction using up and down probabilities on tree on
            // previous conversion probabilities, ie weighted average
            // of previous probabilities.
            newConversionProbability[j] = (pd * conversionProbability[j]) + (pu * conversionProbability[j + 1]);

            // Use blended discounting rate
            newSpreadAdjustedRate[j] = (newConversionProbability[j] * riskFreeRate)
                + ((1 - newConversionProbability[j]) * (riskFreeRate + CreditSpread));

            newValues[j] = (pd * values[j] / (1 + (spreadAdjustedRate[j] * dt)))
                + (pu * values[j + 1] / (1 + (spreadAdjustedRate[j + 1] * dt)));
        }
    }
}
