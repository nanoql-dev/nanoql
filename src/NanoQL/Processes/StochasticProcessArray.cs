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

    /// <inheritdoc/>
    public override Vector Drift(double t, Vector x)
    {
        ArgumentNullException.ThrowIfNull(x);
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
    public override Matrix Diffusion(double t, Vector x)
    {
        ArgumentNullException.ThrowIfNull(x);
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
    public override Vector Expectation(double t0, Vector x0, double dt)
    {
        ArgumentNullException.ThrowIfNull(x0);
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
    public override Matrix StdDeviation(double t0, Vector x0, double dt)
    {
        ArgumentNullException.ThrowIfNull(x0);
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
    public override Vector Evolve(double t0, Vector x0, double dt, Vector dw)
    {
        ArgumentNullException.ThrowIfNull(x0);
        ArgumentNullException.ThrowIfNull(dw);
        Vector dz = sqrtCorrelation * dw;

        var tmp = new Vector(Size);
        for (int i = 0; i < Size; ++i)
        {
            tmp[i] = processes[i].Evolve(t0, x0[i], dt, dz[i]);
        }

        return tmp;
    }

    /// <summary>The changed state, each process applying its own element's change.</summary>
    /// <param name="x0">The state, one element per process.</param>
    /// <param name="dx">The change, one element per process.</param>
    /// <returns>A new vector.</returns>
    public override Vector Apply(Vector x0, Vector dx)
    {
        ArgumentNullException.ThrowIfNull(x0);
        ArgumentNullException.ThrowIfNull(dx);
        var tmp = new Vector(Size);
        for (int i = 0; i < Size; ++i)
        {
            tmp[i] = processes[i].Apply(x0[i], dx[i]);
        }

        return tmp;
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
