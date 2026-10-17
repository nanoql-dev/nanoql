// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/montecarlo/pathgenerator.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// Generates random paths of a 1-D stochastic process using a Gaussian sequence generator: each draw evolves the
/// process from its initial value over the time grid, one variate per step.
/// </summary>
/// <remarks>
/// <para>
/// A working type: mutable, owned by one computation and not thread-safe. Each draw changes it. A draw's sample holds
/// the generator's own path, a view valid until the next draw, which overwrites it; a caller that keeps a path copies
/// it. No draw allocates.
/// </para>
/// <para>
/// It copies the sequence generator it is given, so the caller's later draws from that generator change nothing in
/// its paths, and <see cref="Clone"/> copies it deeply. With the Brownian bridge, each sequence is first transformed
/// into the variations of a bridge over the time grid.
/// </para>
/// <para>
/// The named shortcut classes derive from it; nanoql's own code uses the generic type. QuantLib takes any process and
/// casts it to a 1-D one, failing later on a null; nanoql refuses a process that isn't 1-D when it is built.
/// </para>
/// </remarks>
/// <typeparam name="TGsg">
/// The Gaussian sequence generator's type, whose <see cref="IRsg.Clone"/> returns a <typeparamref name="TGsg"/>, as
/// every generator's does.
/// </typeparam>
public class PathGenerator<TGsg>
    where TGsg : class, IRsg
{
    private readonly bool brownianBridge;
    private readonly TGsg generator;
    private readonly int dimension;
    private readonly TimeGrid timeGrid;
    private readonly StochasticProcess1D process;
    private readonly MonteCarloPath path;
    private readonly double[] temp;
    private readonly BrownianBridge bb;

    /// <summary>
    /// Initializes a new instance of the <see cref="PathGenerator{TGsg}"/> class over a regular time grid from 0 to
    /// <paramref name="length"/>.
    /// </summary>
    /// <param name="process">The process, which must be 1-D.</param>
    /// <param name="length">The length of the paths, positive.</param>
    /// <param name="timeSteps">The number of steps, at least 1: the generator's dimension.</param>
    /// <param name="generator">The Gaussian sequence generator, which the path generator copies.</param>
    /// <param name="brownianBridge">Whether to build the paths as Brownian bridges.</param>
    /// <exception cref="ArgumentException">
    /// The process isn't a <see cref="StochasticProcess1D"/>, the length isn't positive, or the generator's dimension
    /// isn't <paramref name="timeSteps"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="timeSteps"/> is less than 1, for which QuantLib's generator is undefined.
    /// </exception>
    public PathGenerator(StochasticProcess process, double length, int timeSteps, TGsg generator, bool brownianBridge)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(generator);
        this.process = OneDimensional(process);

        // QuantLib's member initialisers build timeGrid_(length, timeSteps) first, so its refusal of the length comes
        // first; its Size can't be negative. It then builds its Brownian bridge over the grid before checking the
        // dimension, undefined with no step.
        if (!(timeSteps >= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(timeSteps), timeSteps, StepsMessage(timeSteps));
        }

        timeGrid = new TimeGrid(length, timeSteps);
        if (!(timeSteps >= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(timeSteps), timeSteps, StepsMessage(timeSteps));
        }

        this.brownianBridge = brownianBridge;
        this.generator = (TGsg)generator.Clone();
        dimension = this.generator.Dimension;
        if (!(dimension == timeSteps))
        {
            throw new ArgumentException(
                $"sequence generator dimensionality ({dimension}) != timeSteps ({timeSteps})",
                nameof(generator));
        }

        path = MonteCarloPath.ForGenerator(timeGrid);
        temp = new double[dimension];
        bb = new BrownianBridge(timeGrid);
    }

    /// <summary>Initializes a new instance of the <see cref="PathGenerator{TGsg}"/> class over a time grid.</summary>
    /// <param name="process">The process, which must be 1-D.</param>
    /// <param name="timeGrid">The time grid, of at least two times, whose steps number the generator's dimension.</param>
    /// <param name="generator">The Gaussian sequence generator, which the path generator copies.</param>
    /// <param name="brownianBridge">Whether to build the paths as Brownian bridges.</param>
    /// <exception cref="ArgumentException">
    /// The process isn't a <see cref="StochasticProcess1D"/>, or the generator's dimension isn't the number of the
    /// grid's steps.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="timeGrid"/> has fewer than two times, for which QuantLib's generator is undefined.
    /// </exception>
    public PathGenerator(StochasticProcess process, TimeGrid timeGrid, TGsg generator, bool brownianBridge)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(timeGrid);
        ArgumentNullException.ThrowIfNull(generator);
        this.process = OneDimensional(process);

        // QuantLib builds its Brownian bridge over the grid before checking the dimension, undefined with no step.
        if (!(timeGrid.Size >= 2))
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeGrid),
                $"a path generator needs a time grid of at least two times, not {timeGrid.Size}");
        }

        this.brownianBridge = brownianBridge;
        this.generator = (TGsg)generator.Clone();
        dimension = this.generator.Dimension;
        this.timeGrid = timeGrid;
        if (!(dimension == timeGrid.Size - 1))
        {
            throw new ArgumentException(
                $"sequence generator dimensionality ({dimension}) != timeSteps ({timeGrid.Size - 1})",
                nameof(generator));
        }

        path = MonteCarloPath.ForGenerator(timeGrid);
        temp = new double[dimension];
        bb = new BrownianBridge(timeGrid);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PathGenerator{TGsg}"/> class from a copy of another, for a
    /// subclass's <see cref="Clone"/>: QuantLib's copy constructor.
    /// </summary>
    /// <param name="source">The path generator to copy: its sequence generator deeply, its path and buffer by value;
    /// its time grid, Brownian bridge and process, which are immutable, shared.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    protected PathGenerator(PathGenerator<TGsg> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        brownianBridge = source.brownianBridge;
        generator = (TGsg)source.generator.Clone();
        dimension = source.dimension;
        timeGrid = source.timeGrid;
        process = source.process;
        path = MonteCarloPath.ForGenerator(timeGrid);
        source.path.Buffer.CopyTo(path.Buffer);
        temp = (double[])source.temp.Clone();
        bb = source.bb;
    }

    /// <summary>Gets the number of steps, the sequence generator's dimension.</summary>
    public int Size => dimension;

    /// <summary>Gets the time grid of the paths.</summary>
    public TimeGrid TimeGrid => timeGrid;

    /// <summary>Draws the next path from the next sequence, changing the generator.</summary>
    /// <returns>The path, the generator's own, valid until its next draw, and the sequence's weight.</returns>
    public Sample<MonteCarloPath> Next() => Next(false);

    /// <summary>Draws the antithetic of the last path: the same sequence, its variates negated.</summary>
    /// <returns>The path, the generator's own, valid until its next draw, and the sequence's weight.</returns>
    public Sample<MonteCarloPath> Antithetic() => Next(true);

    /// <summary>
    /// Copies the path generator deeply, as QuantLib's copy constructor does: the copy's next path, and antithetic,
    /// equal this one's, into its own path, and drawing from either leaves the other unchanged.
    /// </summary>
    /// <returns>The copy.</returns>
    public virtual PathGenerator<TGsg> Clone() => new(this);

    private static string StepsMessage(int timeSteps) => $"a path generator needs at least one time step, not {timeSteps}";

    private static StochasticProcess1D OneDimensional(StochasticProcess process) =>
        process as StochasticProcess1D
        ?? throw new ArgumentException(
            $"a path generator needs a 1-D process (StochasticProcess1D), not a {process.Size}-D {process.GetType().Name}",
            nameof(process));

    private Sample<MonteCarloPath> Next(bool antithetic)
    {
        Sample<ReadOnlyMemory<double>> sequence = antithetic ? generator.LastSequence() : generator.NextSequence();

        if (brownianBridge)
        {
            bb.Transform(sequence.Value.Span, temp);
        }

        ReadOnlySpan<double> increments = brownianBridge ? temp : sequence.Value.Span;

        Span<double> values = path.Buffer;
        values[0] = process.X0;

        for (int i = 1; i < values.Length; i++)
        {
            double t = timeGrid[i - 1];
            double dt = timeGrid.Dt(i - 1);
            values[i] = process.Evolve(t, values[i - 1], dt, antithetic ? -increments[i - 1] : increments[i - 1]);
        }

        return new Sample<MonteCarloPath>(path, sequence.Weight);
    }
}
