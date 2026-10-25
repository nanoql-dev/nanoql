// SPDX-FileCopyrightText: Copyright (C) 2026 Colin Alberts
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/equity/roughhestonmodel.hpp ql/models/equity/roughhestonmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.Models;

/// <summary>The rough Heston model for the stochastic volatility of an asset: the Heston parameters and the variance's
/// Hurst exponent, calibrated.</summary>
/// <remarks>
/// <para>
/// Its arguments are θ, κ, σ, ρ, v₀ and the Hurst exponent H, in that order, each a constant parameter: θ, κ, σ and v₀
/// positive, ρ in [−1, 1], H in [0, 0.5]. Immutable: <see cref="CalibratedModel.WithParams"/> returns a new model, whose
/// process <see cref="GenerateArguments"/> builds from the given process's curves and spot and the new parameters; the
/// constructor keeps the given process, as QuantLib's does.
/// </para>
/// <para>
/// References: O. El Euch and M. Rosenbaum, The characteristic function of rough Heston models, Mathematical Finance
/// 29(1), 3-38 (2019); J. Gatheral, T. Jaisson and M. Rosenbaum, Volatility is rough, Quantitative Finance 18(6),
/// 933-949 (2018).
/// </para>
/// </remarks>
public sealed class RoughHestonModel : CalibratedModel
{
    /// <summary>Initializes a new instance of the <see cref="RoughHestonModel"/> class.</summary>
    /// <param name="process">The process whose curves, spot and parameters the model starts from.</param>
    /// <param name="hurst">The variance's Hurst exponent, in [0, 0.5].</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">θ, κ, σ or v₀ isn't positive, ρ is outside [−1, 1], or
    /// <paramref name="hurst"/> is outside [0, 0.5].</exception>
    public RoughHestonModel(HestonProcess process, double hurst)
        : base(6)
    {
        ArgumentNullException.ThrowIfNull(process);
        Process = process;
        SetArgument(0, new ConstantParameter(process.Theta, new PositiveConstraint(), nameof(process)));
        SetArgument(1, new ConstantParameter(process.Kappa, new PositiveConstraint(), nameof(process)));
        SetArgument(2, new ConstantParameter(process.Sigma, new PositiveConstraint(), nameof(process)));
        SetArgument(3, new ConstantParameter(process.Rho, new BoundaryConstraint(-1.0, 1.0), nameof(process)));
        SetArgument(4, new ConstantParameter(process.V0, new PositiveConstraint(), nameof(process)));
        SetArgument(5, new ConstantParameter(hurst, new BoundaryConstraint(0.0, 0.5), nameof(hurst)));
    }

    /// <summary>Gets the variance's long-run mean θ.</summary>
    public double Theta => Arguments[0].Evaluate(0.0);

    /// <summary>Gets the variance's mean-reversion speed κ.</summary>
    public double Kappa => Arguments[1].Evaluate(0.0);

    /// <summary>Gets the variance's volatility σ.</summary>
    public double Sigma => Arguments[2].Evaluate(0.0);

    /// <summary>Gets the correlation ρ.</summary>
    public double Rho => Arguments[3].Evaluate(0.0);

    /// <summary>Gets the variance's value v₀.</summary>
    public double V0 => Arguments[4].Evaluate(0.0);

    /// <summary>Gets the variance's Hurst exponent H.</summary>
    public double Hurst => Arguments[5].Evaluate(0.0);

    /// <summary>Gets the underlying process: the given one, or on a model <see cref="CalibratedModel.WithParams"/>
    /// returns, one on the model's parameters.</summary>
    public HestonProcess Process { get; private set; }

    /// <summary>The process, from the given process's curves and spot and the model's parameters.</summary>
    protected override void GenerateArguments() =>
        Process = new HestonProcess(Process.RiskFreeRate, Process.DividendYield, Process.S0, V0, Kappa, Theta, Sigma, Rho);
}
