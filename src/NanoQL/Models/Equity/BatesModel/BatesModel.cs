// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/equity/batesmodel.hpp ql/models/equity/batesmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.Models;

/// <summary>The Bates stochastic-volatility model: the Heston model with log-normal jumps, calibrated.</summary>
/// <remarks>
/// <para>
/// Its arguments are the Heston model's five, then ν (unconstrained), δ and λ (positive), in QuantLib's order; its
/// process a <see cref="BatesProcess"/> on the model's parameters, with QuantLib's default discretization (full
/// truncation), built at construction and on the copy <see cref="CalibratedModel.WithParams"/> returns. A
/// <see cref="HestonModel"/>, as QuantLib's derives from it, so the Heston engines and
/// <see cref="HestonModelHelper"/> take it; a Heston-only engine prices its Heston part.
/// </para>
/// <para>
/// An extension point: <see cref="BatesDetJumpModel"/> derives from it. The constructor builds its process through a
/// non-virtual member, QuantLib's <c>BatesModel::generateArguments()</c>.
/// </para>
/// <para>
/// References: A. Sepp, Pricing European-Style Options under Jump Diffusion Processes with Stochastic Volatility:
/// Applications of Fourier Transform.
/// </para>
/// </remarks>
public class BatesModel : HestonModel
{
    /// <summary>Initializes a new instance of the <see cref="BatesModel"/> class.</summary>
    /// <param name="process">The process whose curves, spot and parameters the model starts from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A Heston parameter is outside its constraint, or δ or λ isn't
    /// positive.</exception>
    public BatesModel(BatesProcess process)
        : this(process, 8)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BatesModel"/> class with room for a derived model's arguments.
    /// </summary>
    /// <param name="process">The process whose curves, spot and parameters the model starts from.</param>
    /// <param name="nArguments">The number of arguments, at least 8.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="nArguments"/> is below 8.</exception>
    /// <exception cref="ArgumentException">A Heston parameter is outside its constraint, or δ or λ isn't
    /// positive.</exception>
    protected BatesModel(BatesProcess process, int nArguments)
        : base(process, Checked(nArguments))
    {
        SetArgument(5, new ConstantParameter(process.Nu, new NoConstraint(), nameof(process)));
        SetArgument(6, new ConstantParameter(process.Delta, new PositiveConstraint(), nameof(process)));
        SetArgument(7, new ConstantParameter(process.Lambda, new PositiveConstraint(), nameof(process)));

        BuildBatesProcess();
    }

    /// <summary>Gets the log jump size's mean ν.</summary>
    public double Nu => Arguments[5].Evaluate(0.0);

    /// <summary>Gets the log jump size's standard deviation δ.</summary>
    public double Delta => Arguments[6].Evaluate(0.0);

    /// <summary>Gets the jumps' intensity λ.</summary>
    public double Lambda => Arguments[7].Evaluate(0.0);

    /// <summary>The process, a <see cref="BatesProcess"/> from the given process's curves and spot and the model's
    /// parameters.</summary>
    protected override void GenerateArguments() => BuildBatesProcess();

    private static int Checked(int nArguments)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(nArguments, 8);
        return nArguments;
    }

    // QuantLib's BatesModel::generateArguments, non-virtual for the constructor
    private void BuildBatesProcess() =>
        Process = new BatesProcess(
            Process.RiskFreeRate, Process.DividendYield, Process.S0, V0, Kappa, Theta, Sigma, Rho, Lambda, Nu, Delta);
}
