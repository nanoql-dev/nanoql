// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/equity/hestonmodel.hpp ql/models/equity/hestonmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.Models;

/// <summary>The Heston model for the stochastic volatility of an asset: its process's five parameters,
/// calibrated.</summary>
/// <remarks>
/// <para>
/// Its arguments are θ, κ, σ, ρ and v₀, in that order, each a constant parameter: θ, κ, σ and v₀ positive, ρ in
/// [−1, 1]. Immutable: <see cref="CalibratedModel.WithParams"/> returns a new model, whose process
/// <see cref="GenerateArguments"/> builds from the new parameters.
/// </para>
/// <para>
/// As QuantLib's, the model's process is built afresh from the given process's curves and spot and the model's
/// parameters, with the default discretization whatever the given process's; QuantLib's engines read only its curves,
/// its spot and the parameters.
/// </para>
/// <para>
/// An extension point, as in QuantLib: the Bates models derive from it (the S-hes plan's HR1), through the protected
/// constructor, which sets the five Heston arguments of a larger argument list and builds the Heston process. No
/// constructor calls the virtual <see cref="GenerateArguments"/>, which in C# would run a derived override before the
/// derived constructor has set its arguments; each builds its process through its own non-virtual member, as QuantLib's
/// C++ base constructor runs its own <c>generateArguments</c>.
/// </para>
/// </remarks>
public class HestonModel : CalibratedModel
{
    /// <summary>Initializes a new instance of the <see cref="HestonModel"/> class.</summary>
    /// <param name="process">The process whose curves, spot and parameters the model starts from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">θ, κ, σ or v₀ isn't positive, or ρ is outside [−1, 1].</exception>
    public HestonModel(HestonProcess process)
        : this(process, 5)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HestonModel"/> class with room for a derived model's arguments:
    /// the five Heston arguments set from the process, the Heston process built from them, the rest for the derived
    /// constructor to set.
    /// </summary>
    /// <param name="process">The process whose curves, spot and parameters the model starts from.</param>
    /// <param name="nArguments">The number of arguments, at least 5.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="nArguments"/> is below 5.</exception>
    /// <exception cref="ArgumentException">θ, κ, σ or v₀ isn't positive, or ρ is outside [−1, 1].</exception>
    protected HestonModel(HestonProcess process, int nArguments)
        : base(nArguments)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentOutOfRangeException.ThrowIfLessThan(nArguments, 5);
        Process = process;
        SetArgument(0, new ConstantParameter(process.Theta, new PositiveConstraint(), nameof(process)));
        SetArgument(1, new ConstantParameter(process.Kappa, new PositiveConstraint(), nameof(process)));
        SetArgument(2, new ConstantParameter(process.Sigma, new PositiveConstraint(), nameof(process)));
        SetArgument(3, new ConstantParameter(process.Rho, new BoundaryConstraint(-1.0, 1.0), nameof(process)));
        SetArgument(4, new ConstantParameter(process.V0, new PositiveConstraint(), nameof(process)));
        BuildHestonProcess();
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

    /// <summary>Gets the underlying process, on the model's parameters.</summary>
    /// <remarks>Its setter is for a derived model's constructor and <see cref="GenerateArguments"/> only, on a model
    /// under construction or the copy <see cref="CalibratedModel.WithParams"/> returns.</remarks>
    public HestonProcess Process { get; protected set; }

    /// <summary>The process, from the given process's curves and spot and the model's parameters.</summary>
    protected override void GenerateArguments() => BuildHestonProcess();

    // QuantLib's HestonModel::generateArguments, non-virtual for the constructor
    private void BuildHestonProcess() =>
        Process = new HestonProcess(Process.RiskFreeRate, Process.DividendYield, Process.S0, V0, Kappa, Theta, Sigma, Rho);
}
