// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/processes/stochasticprocessarray.hpp ql/processes/stochasticprocessarray.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Time;
using static NanoQL.Numerics.MatrixFunctions;
using static NanoQL.Numerics.PseudoSqrtFunctions;

namespace NanoQL.Processes;

/// <summary>An array of correlated one-dimensional stochastic processes.</summary>
/// <remarks>
/// The correlation's pseudo square root is taken at construction by the spectral salvaging algorithm, as QuantLib's;
/// each process evolves on its row of the correlated increments. The array's time is its first process's.
/// </remarks>
public sealed class StochasticProcessArray : StochasticProcess
{
    private readonly ImmutableArray<StochasticProcess1D> processes;
    private readonly Matrix sqrtCorrelation;

    /// <summary>Initializes a new instance of the <see cref="StochasticProcessArray"/> class.</summary>
    /// <param name="processes">The one-dimensional processes.</param>
    /// <param name="correlation">Their correlation matrix, symmetric, of the processes' number.</param>
    /// <exception cref="ArgumentException">
    /// The correlation matrix isn't square or is empty (<see cref="PseudoSqrtFunctions.PseudoSqrt"/>), there's no
    /// process, the matrix's size differs from the processes' number, or a process is null.
    /// </exception>
    public StochasticProcessArray(ImmutableArray<StochasticProcess1D> processes, Matrix correlation)
    {
        ArgumentNullException.ThrowIfNull(correlation);
        // QuantLib's member initialisers take the pseudo square root first, so its refusals come first.
        sqrtCorrelation = PseudoSqrt(correlation, SalvagingAlgorithmType.Spectral);

        if (!(!processes.IsDefaultOrEmpty))
        {
            throw new ArgumentException("no processes given", nameof(processes));
        }

        if (!(correlation.Rows == processes.Length))
        {
            throw new ArgumentException(
                "mismatch between number of processes and size of correlation matrix", nameof(correlation));
        }

        foreach (StochasticProcess1D process in processes)
        {
            if (process is null)
            {
                throw new ArgumentException("null 1-D stochastic process", nameof(processes));
            }
        }

        this.processes = processes;
    }

    // The array on other processes with this one's correlation root, as it stands: the basket engines' processes on
    // their clock, with no second pseudo square root, so the root's bits are the array's.
    private StochasticProcessArray(Matrix sqrtCorrelation, ImmutableArray<StochasticProcess1D> processes)
    {
        this.processes = processes;
        this.sqrtCorrelation = sqrtCorrelation;
    }

    /// <inheritdoc/>
    public override int Size => processes.Length;

    /// <inheritdoc/>
    public override Vector InitialValues()
    {
        var tmp = new Vector(Size);
        for (int i = 0; i < Size; ++i)
        {
            tmp[i] = processes[i].X0;
        }

        return tmp;
    }

    /// <summary>The initial values, each process's, written into the caller's span; nothing is allocated.</summary>
    /// <param name="result">The values, of the array's <see cref="Size"/> at least.</param>
    /// <exception cref="IndexOutOfRangeException"><paramref name="result"/> is shorter than the array.</exception>
    public override void InitialValues(Span<double> result)
    {
        for (int i = 0; i < Size; ++i)
        {
            result[i] = processes[i].X0;
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// <paramref name="x"/> holds fewer values than the processes, where QuantLib reads past its end.
    /// </exception>
    public override Vector Drift(double t, Vector x)
    {
        ArgumentNullException.ThrowIfNull(x);
        CheckState(x, nameof(x));
        var tmp = new Vector(Size);
        for (int i = 0; i < Size; ++i)
        {
            tmp[i] = processes[i].Drift(t, x[i]);
        }

        return tmp;
    }

    /// <summary>The diffusion: the correlation's square root, each row times its process's diffusion.</summary>
    /// <param name="t">The time.</param>
    /// <param name="x">The state, one element per process.</param>
    /// <returns>A new matrix.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="x"/> holds fewer values than the processes, where QuantLib reads past its end.
    /// </exception>
    public override Matrix Diffusion(double t, Vector x)
    {
        ArgumentNullException.ThrowIfNull(x);
        CheckState(x, nameof(x));
        Matrix tmp = sqrtCorrelation.Clone();
        for (int i = 0; i < Size; ++i)
        {
            double sigma = processes[i].Diffusion(t, x[i]);
            Span<double> row = tmp.Row(i);
            for (int j = 0; j < row.Length; j++)
            {
                row[j] = row[j] * sigma;
            }
        }

        return tmp;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// <paramref name="x0"/> holds fewer values than the processes, where QuantLib reads past its end.
    /// </exception>
    public override Vector Expectation(double t0, Vector x0, double dt)
    {
        ArgumentNullException.ThrowIfNull(x0);
        CheckState(x0, nameof(x0));
        var tmp = new Vector(Size);
        for (int i = 0; i < Size; ++i)
        {
            tmp[i] = processes[i].Expectation(t0, x0[i], dt);
        }

        return tmp;
    }

    /// <summary>
    /// The standard deviation: the correlation's square root, each row times its process's standard deviation.
    /// </summary>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start, one element per process.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>A new matrix.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="x0"/> holds fewer values than the processes, where QuantLib reads past its end.
    /// </exception>
    public override Matrix StdDeviation(double t0, Vector x0, double dt)
    {
        ArgumentNullException.ThrowIfNull(x0);
        CheckState(x0, nameof(x0));
        Matrix tmp = sqrtCorrelation.Clone();
        for (int i = 0; i < Size; ++i)
        {
            double sigma = processes[i].StdDeviation(t0, x0[i], dt);
            Span<double> row = tmp.Row(i);
            for (int j = 0; j < row.Length; j++)
            {
                row[j] = row[j] * sigma;
            }
        }

        return tmp;
    }

    /// <summary>The covariance: the standard deviation times its transpose.</summary>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start, one element per process.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>A new matrix.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="x0"/> holds fewer values than the processes, where QuantLib reads past its end.
    /// </exception>
    public override Matrix Covariance(double t0, Vector x0, double dt)
    {
        Matrix tmp = StdDeviation(t0, x0, dt);
        return tmp * Transpose(tmp);
    }

    /// <summary>
    /// The state after a time interval: the increments correlated by the correlation's square root, each process
    /// evolved by its own.
    /// </summary>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start, one element per process.</param>
    /// <param name="dt">The interval's length.</param>
    /// <param name="dw">The independent Brownian increments, one per process.</param>
    /// <returns>A new vector.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="x0"/> holds fewer values than the processes, where QuantLib reads past its end.
    /// </exception>
    public override Vector Evolve(double t0, Vector x0, double dt, Vector dw)
    {
        ArgumentNullException.ThrowIfNull(x0);
        ArgumentNullException.ThrowIfNull(dw);
        CheckState(x0, nameof(x0));
        Vector dz = sqrtCorrelation * dw;

        var tmp = new Vector(Size);
        for (int i = 0; i < Size; ++i)
        {
            tmp[i] = processes[i].Evolve(t0, x0[i], dt, dz[i]);
        }

        return tmp;
    }

    /// <summary>
    /// The state after a time interval, written into the caller's span with nothing allocated: the allocating form's
    /// arithmetic, the correlated increments written row by row into <paramref name="result"/> in the order of the
    /// matrix product, then each process evolved on its own entry in place.
    /// </summary>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start, one element per process.</param>
    /// <param name="dt">The interval's length.</param>
    /// <param name="dw">The independent Brownian increments, one per process.</param>
    /// <param name="result">The state at the interval's end, of the array's size at least; it mustn't overlap
    /// <paramref name="x0"/> or <paramref name="dw"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="dw"/>'s length isn't the correlation's.</exception>
    public override void Evolve(double t0, ReadOnlySpan<double> x0, double dt, ReadOnlySpan<double> dw, Span<double> result)
    {
        int n = sqrtCorrelation.Columns;
        if (!(dw.Length == n))
        {
            throw new ArgumentException(
                $"vectors and matrices with different sizes ({dw.Length}, {sqrtCorrelation.Rows}x{n}) cannot be multiplied",
                nameof(dw));
        }

        // dz = sqrtCorrelation * dw, Matrix * Vector's sums
        for (int i = 0; i < Size; ++i)
        {
            ReadOnlySpan<double> row = sqrtCorrelation.Row(i);
            double sum = 0.0;
            for (int k = 0; k < n; ++k)
            {
                sum = sum + (dw[k] * row[k]);
            }

            result[i] = sum;
        }

        for (int i = 0; i < Size; ++i)
        {
            result[i] = processes[i].Evolve(t0, x0[i], dt, result[i]);
        }
    }

    // The array with each Black-Scholes process on a clock (GeneralizedBlackScholesProcess.OnClock) and the other
    // processes as they stand, its correlation root kept: the basket engines' paths on the clock spanning every
    // process's structures; the array itself where no process moves.
    internal StochasticProcessArray OnClock(TermStructure clock)
    {
        StochasticProcess1D[] onClock = new StochasticProcess1D[processes.Length];
        bool moved = false;
        for (int i = 0; i < processes.Length; ++i)
        {
            onClock[i] = processes[i] is GeneralizedBlackScholesProcess bs ? bs.OnClock(clock) : processes[i];
            moved |= !ReferenceEquals(onClock[i], processes[i]);
        }

        return moved ? new StochasticProcessArray(sqrtCorrelation, [.. onClock]) : this;
    }

    /// <summary>The changed state, each process applying its own element's change.</summary>
    /// <param name="x0">The state, one element per process.</param>
    /// <param name="dx">The change, one element per process.</param>
    /// <returns>A new vector.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="x0"/> or <paramref name="dx"/> holds fewer values than the processes, where QuantLib reads past
    /// its end.
    /// </exception>
    public override Vector Apply(Vector x0, Vector dx)
    {
        ArgumentNullException.ThrowIfNull(x0);
        ArgumentNullException.ThrowIfNull(dx);
        CheckState(x0, nameof(x0));
        CheckState(dx, nameof(dx));
        var tmp = new Vector(Size);
        for (int i = 0; i < Size; ++i)
        {
            tmp[i] = processes[i].Apply(x0[i], dx[i]);
        }

        return tmp;
    }

    // A state holds a value per process, QuantLib reading one for each: a shorter one is refused.
    private void CheckState(Vector x, string paramName)
    {
        if (!(x.Size >= Size))
        {
            throw new ArgumentException($"{x.Size} values given for {Size} processes", paramName);
        }
    }

    /// <summary>The time a date denotes: the first process's.</summary>
    /// <param name="date">The date.</param>
    /// <returns>The time.</returns>
    /// <exception cref="ArgumentException"><paramref name="date"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The first process doesn't convert dates to times.</exception>
    public override double Time(Date date) => processes[0].Time(date);

    /// <summary>The i-th process.</summary>
    /// <param name="i">The index.</param>
    /// <returns>The process.</returns>
    public StochasticProcess1D Process(int i) => processes[i];

    /// <summary>The correlation the array's square root gives back, S·Sᵀ.</summary>
    /// <returns>A new matrix.</returns>
    public Matrix Correlation() => sqrtCorrelation * Transpose(sqrtCorrelation);
}
