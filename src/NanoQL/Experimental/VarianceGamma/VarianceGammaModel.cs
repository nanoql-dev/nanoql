// SPDX-FileCopyrightText: Copyright (C) 2010 Adrian O' Neill
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/variancegamma/variancegammamodel.hpp ql/experimental/variancegamma/variancegammamodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The variance gamma model (Madan, Carr and Chang, 1998, "The variance gamma process and option pricing", European
/// Finance Review 2, 79-105): its process's three parameters.
/// </summary>
/// <remarks>
/// Its arguments are σ, ν and θ, in that order, each a constant parameter: σ and ν positive, θ free. Immutable:
/// <see cref="CalibratedModel.WithParams"/> returns a new model, whose process <see cref="GenerateArguments"/> builds
/// from the given process's spot and curves and the new parameters. QuantLib doesn't implement its calibration (no
/// helper prices on it).
/// </remarks>
[Experimental("NQL0001")]
public sealed class VarianceGammaModel : CalibratedModel
{
    /// <summary>Initializes a new instance of the <see cref="VarianceGammaModel"/> class.</summary>
    /// <param name="process">The process whose spot, curves and parameters the model starts from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">σ or ν isn't positive.</exception>
    public VarianceGammaModel(VarianceGammaProcess process)
        : base(3)
    {
        ArgumentNullException.ThrowIfNull(process);
        Process = process;
        SetArgument(0, new ConstantParameter(process.Sigma, new PositiveConstraint(), nameof(process)));
        SetArgument(1, new ConstantParameter(process.Nu, new PositiveConstraint(), nameof(process)));
        SetArgument(2, new ConstantParameter(process.Theta, new NoConstraint(), nameof(process)));
        BuildProcess();
    }

    /// <summary>Gets the Brownian motion's volatility σ.</summary>
    public double Sigma => Arguments[0].Evaluate(0.0);

    /// <summary>Gets the gamma process's variance rate ν.</summary>
    public double Nu => Arguments[1].Evaluate(0.0);

    /// <summary>Gets the Brownian motion's drift θ.</summary>
    public double Theta => Arguments[2].Evaluate(0.0);

    /// <summary>Gets the underlying process, on the model's parameters.</summary>
    public VarianceGammaProcess Process { get; private set; }

    /// <summary>The process, from the given process's spot and curves and the model's parameters.</summary>
    protected override void GenerateArguments() => BuildProcess();

    // QuantLib's VarianceGammaModel::generateArguments, non-virtual for the constructor
    private void BuildProcess() =>
        Process = new VarianceGammaProcess(Process.S0, Process.DividendYield, Process.RiskFreeRate, Sigma, Nu, Theta);
}
