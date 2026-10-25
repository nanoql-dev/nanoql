// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/browniangenerators/sobolbrowniangenerator.hpp ql/models/marketmodels/browniangenerators/sobolbrowniangenerator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Methods;

namespace NanoQL.Models;

/// <summary>
/// The incremental Brownian generator on a low-discrepancy sequence: each path's Gaussian variates, of the factors
/// times the steps, assigned to the factors and steps by an ordering and Brownian-bridged along each factor's path.
/// </summary>
/// <remarks>
/// A working type (the S-hes plan's HR3 (iv)). An extension point, as in QuantLib: a derived class supplies the
/// sequence. A step's output length and a path's exhaustion are checked always, QuantLib's extra-safety checks.
/// </remarks>
public abstract class SobolBrownianGeneratorBase : BrownianGenerator
{
    private readonly int factors;
    private readonly int steps;
    private readonly BrownianBridge bridge;
    private readonly ImmutableArray<ImmutableArray<int>> orderedIndices;
    private readonly double[][] bridgedVariates;

    // a factor's variates in its ordered indices' order, held
    private readonly double[] permuted;
    private int lastStep;

    /// <summary>Initializes a new instance of the <see cref="SobolBrownianGeneratorBase"/> class.</summary>
    /// <param name="factors">The number of factors each step draws, at least 1.</param>
    /// <param name="steps">The number of steps each path draws, at least 1.</param>
    /// <param name="ordering">How the variates are assigned to the factors and steps.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="factors"/> or <paramref name="steps"/> is below 1, or <paramref name="ordering"/> isn't a
    /// member of its type, with QuantLib's message.
    /// </exception>
    protected SobolBrownianGeneratorBase(int factors, int steps, SobolBrownianGeneratorBaseOrdering ordering)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(factors);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(steps);
        this.factors = factors;
        this.steps = steps;
        bridge = new BrownianBridge(steps);

        int[][] m = new int[factors][];
        for (int i = 0; i < factors; ++i)
        {
            m[i] = new int[steps];
        }

        switch (ordering)
        {
            case SobolBrownianGeneratorBaseOrdering.Factors:
                FillByFactor(m, factors, steps);
                break;
            case SobolBrownianGeneratorBaseOrdering.Steps:
                FillByStep(m, factors, steps);
                break;
            case SobolBrownianGeneratorBaseOrdering.Diagonal:
                FillByDiagonal(m, factors, steps);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(ordering), ordering, "unknown ordering");
        }

        orderedIndices = [.. m.Select(row => ImmutableArray.Create(row))];
        bridgedVariates = new double[factors][];
        for (int i = 0; i < factors; ++i)
        {
            bridgedVariates[i] = new double[steps];
        }

        permuted = new double[steps];
    }

    /// <inheritdoc/>
    public override int NumberOfFactors => factors;

    /// <inheritdoc/>
    public override int NumberOfSteps => steps;

    /// <summary>Gets each factor's indices into a sequence, one per step: the ordering QuantLib's test interface
    /// exposes.</summary>
    public ImmutableArray<ImmutableArray<int>> OrderedIndices => orderedIndices;

    /// <inheritdoc/>
    public override double NextPath()
    {
        Sample<ReadOnlyMemory<double>> sample = NextSequence();
        ReadOnlySpan<double> value = sample.Value.Span;

        // Brownian-bridge the variates according to the ordered indices
        for (int i = 0; i < factors; ++i)
        {
            ImmutableArray<int> indices = orderedIndices[i];
            for (int j = 0; j < steps; ++j)
            {
                permuted[j] = value[indices[j]];
            }

            bridge.Transform(permuted, bridgedVariates[i]);
        }

        lastStep = 0;
        return sample.Weight;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="output"/>'s length isn't the number of factors.</exception>
    /// <exception cref="InvalidOperationException">The path's steps are drawn.</exception>
    public override double NextStep(Span<double> output)
    {
        if (output.Length != factors)
        {
            throw new ArgumentException("size mismatch", nameof(output));
        }

        if (!(lastStep < steps))
        {
            throw new InvalidOperationException("sequence exhausted");
        }

        for (int i = 0; i < factors; ++i)
        {
            output[i] = bridgedVariates[i][lastStep];
        }

        ++lastStep;
        return 1.0;
    }

    /// <summary>
    /// Bridges given variates as a path draws them: QuantLib's test interface. The variates are one list per
    /// dimension of a sequence, the factors times the steps, each of a value per path.
    /// </summary>
    /// <param name="variates">The variates, one list per dimension, each of one value per path.</param>
    /// <returns>One list per factor of each path's bridged steps, path after path.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="variates"/> or one of its lists is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// There aren't the factors times the steps lists, with QuantLib's message, or they aren't all as long as the
    /// first, which QuantLib reads unchecked.
    /// </exception>
    public ImmutableArray<ImmutableArray<double>> Transform(IReadOnlyList<IReadOnlyList<double>> variates)
    {
        ArgumentNullException.ThrowIfNull(variates);
        int dim = factors * steps;
        if (variates.Count != dim)
        {
            throw new ArgumentException("inconsistent variate vector", nameof(variates));
        }

        int nPaths = variates[0]?.Count ?? throw new ArgumentNullException(nameof(variates));
        foreach (IReadOnlyList<double> v in variates)
        {
            if (v is null)
            {
                throw new ArgumentNullException(nameof(variates));
            }

            if (v.Count != nPaths)
            {
                throw new ArgumentException("every variate list must hold one value per path", nameof(variates));
            }
        }

        double[][] retVal = new double[factors][];
        for (int i = 0; i < factors; ++i)
        {
            retVal[i] = new double[nPaths * steps];
        }

        double[] sample = new double[dim];
        double[] factorSample = new double[steps];
        for (int j = 0; j < nPaths; ++j)
        {
            for (int k = 0; k < dim; ++k)
            {
                sample[k] = variates[k][j];
            }

            for (int i = 0; i < factors; ++i)
            {
                ImmutableArray<int> indices = orderedIndices[i];
                for (int s = 0; s < steps; ++s)
                {
                    factorSample[s] = sample[indices[s]];
                }

                bridge.Transform(factorSample, retVal[i].AsSpan(j * steps, steps));
            }
        }

        return [.. retVal.Select(row => ImmutableArray.Create(row))];
    }

    /// <summary>The next sequence of Gaussian variates, of the factors times the steps.</summary>
    /// <returns>The sequence and its weight, a view valid until the next draw.</returns>
    protected abstract Sample<ReadOnlyMemory<double>> NextSequence();

    private static void FillByFactor(int[][] m, int factors, int steps)
    {
        int counter = 0;
        for (int i = 0; i < factors; ++i)
        {
            for (int j = 0; j < steps; ++j)
            {
                m[i][j] = counter++;
            }
        }
    }

    private static void FillByStep(int[][] m, int factors, int steps)
    {
        int counter = 0;
        for (int j = 0; j < steps; ++j)
        {
            for (int i = 0; i < factors; ++i)
            {
                m[i][j] = counter++;
            }
        }
    }

    // variate 2 is used for the second factor's full path
    private static void FillByDiagonal(int[][] m, int factors, int steps)
    {
        // starting position of the current diagonal
        int i0 = 0, j0 = 0;

        // current position
        int i = 0, j = 0;
        int counter = 0;
        while (counter < factors * steps)
        {
            m[i][j] = counter++;
            if (i == 0 || j == steps - 1)
            {
                // we completed a diagonal and have to start a new one
                if (i0 < factors - 1)
                {
                    // we start the path of the next factor
                    i0 = i0 + 1;
                    j0 = 0;
                }
                else
                {
                    // we move along the path of the last factor
                    i0 = factors - 1;
                    j0 = j0 + 1;
                }

                i = i0;
                j = j0;
            }
            else
            {
                // we move along the diagonal
                i = i - 1;
                j = j + 1;
            }
        }
    }
}
