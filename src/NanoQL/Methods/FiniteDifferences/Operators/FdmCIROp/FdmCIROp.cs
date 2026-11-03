// SPDX-FileCopyrightText: Copyright (C) 2020 Lew Wei Hao
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/fdmcirop.hpp ql/methods/finitedifferences/operators/fdmcirop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.Methods;

/// <summary>
/// The backward operator of a Black-Scholes equity under a Cox-Ingersoll-Ross short rate, dr = κ(θ − r)dt + σ√r·dW₂,
/// d⟨W₁, W₂⟩ = ρ·dt, on a mesher of the log spot in direction 0 and the short rate in direction 1: the equity's drift
/// r − q − ½v and diffusion ½v, the rate's ½σ²r and κ(θ − r), their covariance ρσ_Sσ√r, discounting at the short rate.
/// </summary>
/// <remarks>
/// <para>
/// A working type: its three parts are built at construction; <see cref="SetTime"/> rewrites them in place, and the
/// apply, splitting solves and preconditioner write into the caller's spans through buffers it holds, so a step
/// allocates nothing. Its times are <see cref="FdmBlackScholesOp.Clock"/>'s, the dividend curve and the volatility read
/// on it, each on its own.
/// </para>
/// <para>
/// QuantLib's rates part is σ²r ∂ᵣᵣ where the CIR generator's is ½σ²r ∂ᵣᵣ, and its mixed term 2ρσ_Sσ where the
/// covariance is ρσ_Sσ√r (<c>fdmcirop.cpp:55-60</c>, <c>:74-82</c>): QuantLib's operator prices a rate with twice the
/// CIR variance, and blows up at ρ ≠ 0. Here the generator's coefficients. With a quanto helper the drift is less its
/// adjustment at the grid's rate (<see cref="FdmCIREquityPart"/>'s remarks), where QuantLib's operator takes no helper.
/// </para>
/// </remarks>
[WorkingType]
public sealed class FdmCIROp : FdmLinearOpComposite
{
    private readonly FdmCIREquityPart dxMap;
    private readonly FdmCIRRatesPart dyMap;
    private readonly FdmCIRMixedPart dzMap;

    // apply's direction terms and the preconditioner's first solve, held
    private readonly double[] tempX;
    private readonly double[] tempY;

    /// <summary>Initializes a new instance of the <see cref="FdmCIROp"/> class.</summary>
    /// <param name="mesher">The mesher: the log spot in direction 0, the short rate in direction 1.</param>
    /// <param name="cirProcess">The short rate's process.</param>
    /// <param name="bsProcess">The equity's process, whose dividend curve and volatility the operator reads.</param>
    /// <param name="rho">The correlation ρ of the two factors.</param>
    /// <param name="strike">The strike the Black forward variance is read at.</param>
    /// <param name="quantoHelper">The quanto helper, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException">The mesher or a process is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The mesher has fewer than two directions.</exception>
    public FdmCIROp(
        FdmMesher mesher,
        CoxIngersollRossProcess cirProcess,
        GeneralizedBlackScholesProcess bsProcess,
        double rho,
        double strike,
        FdmQuantoHelper? quantoHelper = null)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentNullException.ThrowIfNull(cirProcess);
        ArgumentNullException.ThrowIfNull(bsProcess);
        TermStructure clock = FdmBlackScholesOp.Clock(bsProcess, quantoHelper);
        dxMap = new FdmCIREquityPart(mesher, bsProcess, strike, clock, quantoHelper);
        dyMap = new FdmCIRRatesPart(mesher, cirProcess.Volatility, cirProcess.Speed, cirProcess.Level);
        dzMap = new FdmCIRMixedPart(mesher, cirProcess, bsProcess, rho, strike, clock);
        tempX = new double[mesher.Layout.Size];
        tempY = new double[mesher.Layout.Size];
    }

    /// <inheritdoc/>
    public override int Size => 2;

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// <paramref name="t2"/> is before <paramref name="t1"/>, or a curve or the volatility can't be read.
    /// </exception>
    /// <exception cref="NanoQLException">A curve or the volatility can't be read.</exception>
    public override void SetTime(double t1, double t2)
    {
        dxMap.SetTime(t1, t2);
        dyMap.SetTime(t1, t2);
        dzMap.SetTime(t1, t2);
    }

    /// <inheritdoc/>
    public override void Apply(ReadOnlySpan<double> r, Span<double> result)
    {
        dxMap.Map.Apply(r, tempX);
        dyMap.Map.Apply(r, tempY);
        dzMap.Map.Apply(r, result);

        // dy + dx + dz
        for (int i = 0; i < r.Length; ++i)
        {
            result[i] = tempY[i] + tempX[i] + result[i];
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException">The direction is above 1, with QuantLib's message.</exception>
    public override void ApplyDirection(int direction, ReadOnlySpan<double> r, Span<double> result)
    {
        if (direction == 0)
        {
            dxMap.Map.Apply(r, result);
        }
        else if (direction == 1)
        {
            dyMap.Map.Apply(r, result);
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "direction too large");
        }
    }

    /// <inheritdoc/>
    public override void ApplyMixed(ReadOnlySpan<double> r, Span<double> result) => dzMap.Map.Apply(r, result);

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException">The direction is above 1, with QuantLib's message.</exception>
    public override void SolveSplitting(int direction, ReadOnlySpan<double> r, double s, Span<double> result)
    {
        if (direction == 0)
        {
            dxMap.Map.SolveSplitting(r, s, 1.0, result);
        }
        else if (direction == 1)
        {
            dyMap.Map.SolveSplitting(r, s, 1.0, result);
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "direction too large");
        }
    }

    /// <inheritdoc/>
    public override void Preconditioner(ReadOnlySpan<double> r, double s, Span<double> result)
    {
        // solve_splitting(1, solve_splitting(0, r, dt), dt)
        SolveSplitting(0, r, s, tempX);
        SolveSplitting(1, tempX, s, result);
    }

    /// <inheritdoc/>
    public override ImmutableArray<SparseMatrix> ToMatrixDecomp() =>
        [dxMap.Map.ToMatrix(), dyMap.Map.ToMatrix(), dzMap.Map.ToMatrix()];
}
