// SPDX-FileCopyrightText: Copyright (C) 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/pathwisegreeks/swaptionpseudojacobian.hpp ql/models/marketmodels/pathwisegreeks/swaptionpseudojacobian.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// A swaption's implied volatility in a market model, by frozen coefficients, and its derivatives with respect to the
/// pseudo-roots' elements, step by step, for market vegas.
/// </summary>
/// <remarks>
/// <para>
/// Immutable (the S-mmp plan's MMPR1 (ii)): computed at construction, as QuantLib's constructor computes; its matrix
/// reads return copies.
/// </para>
/// <para>
/// Refused (MMPR5 (b)), as <see cref="SwapForwardMappings.SwaptionImpliedVolatility"/> refuses, with its texts: steps
/// straddling the swaption's expiry, whose variance QuantLib sums in whole steps (<c>swaptionpseudojacobian.cpp:53</c>),
/// and unequal displacements, of which it reads the first (<c>:44</c>). On every grid holding the expiry, QuantLib's
/// value.
/// </para>
/// </remarks>
public sealed class SwaptionPseudoDerivative
{
    private readonly Matrix[] varianceDerivatives;
    private readonly Matrix[] volatilityDerivatives;

    /// <summary>Initializes a new instance of the <see cref="SwaptionPseudoDerivative"/> class.</summary>
    /// <param name="inputModel">The model.</param>
    /// <param name="startIndex">The swap's first rate.</param>
    /// <param name="endIndex">The swap's end, after its last rate, at most the number of rates.</param>
    /// <exception cref="ArgumentException">
    /// The start index isn't before the end, the model's displacements aren't equal, or its evolution times don't hold
    /// the swaption's expiry.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">An index is outside the rates.</exception>
    public SwaptionPseudoDerivative(MarketModel inputModel, int startIndex, int endIndex)
    {
        ArgumentNullException.ThrowIfNull(inputModel);
        SwapForwardMappings.CheckSwaption(inputModel, startIndex, endIndex);
        EvolutionDescription evolution = inputModel.Evolution;
        ReadOnlySpan<double> subRateTimes = evolution.RateTimes.AsSpan(startIndex, endIndex - startIndex + 1);
        ReadOnlySpan<double> subForwards = inputModel.InitialRates.AsSpan(startIndex, endIndex - startIndex);
        var cs = new LMMCurveState(subRateTimes);
        cs.SetOnForwardRates(subForwards);

        Matrix zed = SwapForwardMappings.CoterminalSwapZedMatrix(cs, inputModel.Displacements[0]);

        int factors = inputModel.NumberOfFactors;
        int numberSteps = evolution.NumberOfSteps;

        // first compute variance and implied vol
        double variance = 0.0;
        int index = 0;
        while (index < numberSteps && evolution.FirstAliveRate[index] <= startIndex)
        {
            Matrix thisPseudo = inputModel.PseudoRoot(index);
            double thisVariance = 0.0;
            for (int j = startIndex; j < endIndex; ++j)
            {
                for (int k = startIndex; k < endIndex; ++k)
                {
                    for (int f = 0; f < factors; ++f)
                    {
                        thisVariance += zed[0, j - startIndex] * thisPseudo[j, f] * thisPseudo[k, f] * zed[0, k - startIndex];
                    }
                }
            }

            variance += thisVariance;
            ++index;
        }

        int stopIndex = index;

        Variance = variance;
        Expiry = subRateTimes[0];
        ImpliedVolatility = Math.Sqrt(variance / Expiry);

        double scale = 0.5 * (1.0 / Expiry) / ImpliedVolatility;

        int numberRates = evolution.NumberOfRates;
        var thisDerivative = new Matrix(numberRates, factors, 0.0);
        var nullDerivative = new Matrix(numberRates, factors, 0.0);
        varianceDerivatives = new Matrix[numberSteps];
        volatilityDerivatives = new Matrix[numberSteps];

        index = 0;
        while (index < stopIndex)
        {
            Matrix thisPseudo = inputModel.PseudoRoot(index);

            for (int rate = startIndex; rate < endIndex; ++rate)
            {
                int zIndex = rate - startIndex;
                for (int f = 0; f < factors; ++f)
                {
                    double sum = 0.0;
                    for (int rate2 = startIndex; rate2 < endIndex; ++rate2)
                    {
                        int zIndex2 = rate2 - startIndex;
                        sum += zed[0, zIndex2] * thisPseudo[rate2, f];
                    }

                    sum *= 2.0 * zed[0, zIndex];
                    thisDerivative[rate, f] = sum;
                }
            }

            varianceDerivatives[index] = thisDerivative.Clone();

            for (int rate = startIndex; rate < endIndex; ++rate)
            {
                for (int f = 0; f < factors; ++f)
                {
                    thisDerivative[rate, f] *= scale;
                }
            }

            volatilityDerivatives[index] = thisDerivative.Clone();
            ++index;
        }

        for (; index < numberSteps; ++index)
        {
            varianceDerivatives[index] = nullDerivative;
            volatilityDerivatives[index] = nullDerivative;
        }
    }

    /// <summary>Gets the swaption's implied volatility.</summary>
    public double ImpliedVolatility { get; }

    /// <summary>Gets the swap rate's variance to the swaption's expiry.</summary>
    public double Variance { get; }

    /// <summary>Gets the swaption's expiry, the start index's rate time.</summary>
    public double Expiry { get; }

    /// <summary>The variance's derivatives with respect to a step's pseudo-root elements.</summary>
    /// <param name="i">The step.</param>
    /// <returns>A copy of the derivatives, a row per rate and a column per factor.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a step.</exception>
    public Matrix VarianceDerivative(int i) => Step(varianceDerivatives, i).Clone();

    /// <summary>The implied volatility's derivatives with respect to a step's pseudo-root elements.</summary>
    /// <param name="i">The step.</param>
    /// <returns>A copy of the derivatives, a row per rate and a column per factor.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a step.</exception>
    public Matrix VolatilityDerivative(int i) => Step(volatilityDerivatives, i).Clone();

    // QuantLib reads its vector unchecked.
    private static Matrix Step(Matrix[] derivatives, int i)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(i, derivatives.Length);
        return derivatives[i];
    }
}
