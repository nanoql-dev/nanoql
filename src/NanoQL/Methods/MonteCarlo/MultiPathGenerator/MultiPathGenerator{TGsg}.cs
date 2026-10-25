// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/montecarlo/multipathgenerator.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>Generates a multi-path from a random-sequence generator.</summary>
/// <typeparam name="TGsg">The Gaussian sequence generator, whose dimension is the factors times the time steps.</typeparam>
/// <remarks>
/// <para>
/// A working type (S2R2), not thread-safe: <see cref="Next()"/> and <see cref="Antithetic"/> draw from its generator
/// and write its own paths, so the sample's value is a view valid until the next draw. It copies the generator it is
/// given. Each step evolves the process over the step's increments through the process's in-place
/// <see cref="StochasticProcess.InitialValues(Span{double})"/> and <see cref="StochasticProcess.Evolve(double,
/// ReadOnlySpan{double}, double, ReadOnlySpan{double}, Span{double})"/>, into buffers the generator holds, where
/// QuantLib's process returns a new array a step: a process that overrides the two steps without allocating (the S-hes
/// plan's HR2), and the loop allocates nothing per sample; one that doesn't keeps its allocating forms' bits.
/// </para>
/// <para>
/// QuantLib refuses a Brownian bridge at the first draw ("Brownian bridge not supported"); this generator refuses it
/// at construction, with the same text.
/// </para>
/// </remarks>
public class MultiPathGenerator<TGsg> : IPathGenerator<MultiPath>
    where TGsg : class, IRsg
{
    private readonly bool brownianBridge;
    private readonly StochasticProcess process;
    private readonly TGsg generator;
    private readonly MultiPath next;

    // the state at a step's start and end, swapped each step, and the step's increments
    private double[] asset;
    private double[] nextAsset;
    private readonly double[] temp;

    /// <summary>Initializes a new instance of the <see cref="MultiPathGenerator{TGsg}"/> class.</summary>
    /// <param name="process">The process.</param>
    /// <param name="times">The time grid, of at least two times.</param>
    /// <param name="generator">The sequence generator, which the path generator copies.</param>
    /// <param name="brownianBridge">Whether a Brownian bridge orders the draws: not supported.</param>
    /// <exception cref="ArgumentOutOfRangeException">The process has no dimension.</exception>
    /// <exception cref="ArgumentException">
    /// The generator's dimension isn't the process's factors times the time steps, the grid has fewer than two times, or
    /// a Brownian bridge is asked for.
    /// </exception>
    public MultiPathGenerator(StochasticProcess process, TimeGrid times, TGsg generator, bool brownianBridge = false)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(times);
        ArgumentNullException.ThrowIfNull(generator);
        this.brownianBridge = brownianBridge;
        this.process = process;
        this.generator = (TGsg)generator.Clone();
        next = MultiPath.ForGenerator(process.Size, times);
        asset = new double[process.Size];
        nextAsset = new double[process.Size];
        temp = new double[process.Factors];

        // times.size()-1 is unsigned: it wraps for an empty grid, which the message shows as QuantLib's does.
        ulong steps = unchecked((ulong)times.Size - 1UL);
        if (!((ulong)this.generator.Dimension == unchecked((ulong)process.Factors * steps)))
        {
            throw new ArgumentException(
                $"dimension ({this.generator.Dimension}) is not equal to ({process.Factors} * {steps}) the number of "
                + "factors times the number of time steps",
                nameof(generator));
        }

        if (!(times.Size > 1))
        {
            throw new ArgumentException("no times given", nameof(times));
        }

        if (brownianBridge)
        {
            throw new ArgumentException("Brownian bridge not supported", nameof(brownianBridge));
        }
    }

    /// <summary>Initializes a new instance of the <see cref="MultiPathGenerator{TGsg}"/> class, a copy of another.</summary>
    /// <param name="source">The generator copied: its sequence generator deeply, its last paths' values too.</param>
    protected MultiPathGenerator(MultiPathGenerator<TGsg> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        brownianBridge = source.brownianBridge;
        process = source.process;
        generator = (TGsg)source.generator.Clone();
        next = MultiPath.ForGenerator(source.next.AssetNumber, source.next[0].TimeGrid);
        asset = new double[source.asset.Length];
        nextAsset = new double[source.nextAsset.Length];
        temp = new double[source.temp.Length];
        for (int j = 0; j < next.AssetNumber; j++)
        {
            source.next[j].Buffer.CopyTo(next[j].Buffer);
        }
    }

    /// <inheritdoc/>
    public Sample<MultiPath> Next() => Next(false);

    /// <inheritdoc/>
    public Sample<MultiPath> Antithetic() => Next(true);

    /// <summary>A copy: the sequence generator copied deeply, so the two draw independently from here on.</summary>
    /// <returns>The copy.</returns>
    public virtual MultiPathGenerator<TGsg> Clone() => new(this);

    private Sample<MultiPath> Next(bool antithetic)
    {
        Sample<ReadOnlyMemory<double>> sequence = antithetic ? generator.LastSequence() : generator.NextSequence();
        ReadOnlySpan<double> draws = sequence.Value.Span;

        int m = process.Size;
        int n = process.Factors;

        MultiPath path = next;

        process.InitialValues(asset);
        for (int j = 0; j < m; j++)
        {
            path[j].Buffer[0] = asset[j];
        }

        TimeGrid timeGrid = path[0].TimeGrid;
        double t, dt;
        for (int i = 1; i < path.PathSize(); i++)
        {
            int offset = (i - 1) * n;
            t = timeGrid[i - 1];
            dt = timeGrid.Dt(i - 1);
            for (int k = 0; k < n; k++)
            {
                temp[k] = antithetic ? -draws[offset + k] : draws[offset + k];
            }

            process.Evolve(t, asset, dt, temp, nextAsset);
            (asset, nextAsset) = (nextAsset, asset);
            for (int j = 0; j < m; j++)
            {
                path[j].Buffer[i] = asset[j];
            }
        }

        return new Sample<MultiPath>(path, sequence.Weight);
    }
}
