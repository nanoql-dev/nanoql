// SPDX-FileCopyrightText: Copyright (C) 2018 Sebastian Schlenkrich
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/basismodels/tenoroptionletvts.hpp ql/experimental/basismodels/tenoroptionletvts.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The two-parameter correlation ρ = ρ∞ + (1 − ρ∞)·exp(−β|t₂ − t₁|), its parameters interpolated in the first start
/// time: QuantLib's <c>TenorOptionletVTS::TwoParameterCorrelation</c>.
/// </summary>
/// <remarks>The interpolations are read at the first time as they are built: past their range only if they allow
/// extrapolation, as QuantLib's.</remarks>
[Experimental("NQL0001")]
public sealed class TwoParameterCorrelation : TenorOptionletVTSCorrelationStructure
{
    private readonly Interpolation rhoInf;
    private readonly Interpolation beta;

    /// <summary>Initializes a new instance of the <see cref="TwoParameterCorrelation"/> class.</summary>
    /// <param name="rhoInf">The long-run correlation ρ∞ by start time.</param>
    /// <param name="beta">The decay β by start time.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public TwoParameterCorrelation(Interpolation rhoInf, Interpolation beta)
    {
        this.rhoInf = rhoInf ?? throw new ArgumentNullException(nameof(rhoInf));
        this.beta = beta ?? throw new ArgumentNullException(nameof(beta));
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">An interpolation can't be read at <paramref name="start1"/>.</exception>
    public override double Evaluate(double start1, double start2)
    {
        double r = rhoInf.Evaluate(start1);
        double b = beta.Evaluate(start1);
        double rho = r + ((1.0 - r) * Math.Exp(-b * Math.Abs(start2 - start1)));
        return rho;
    }
}
