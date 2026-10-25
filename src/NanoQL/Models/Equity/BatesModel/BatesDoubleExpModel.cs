// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/equity/batesmodel.hpp ql/models/equity/batesmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.Models;

/// <summary>
/// The Heston model with double-exponential jumps: an up-jump with probability p, its size exponential with mean
/// η_u (<see cref="NuUp"/>), and a down-jump otherwise, with mean η_d (<see cref="NuDown"/>), at intensity λ.
/// </summary>
/// <remarks>
/// <para>
/// Its arguments are the Heston model's five, then p in [0, 1], η_d and λ positive, and η_u in (0, 1), in QuantLib's
/// order; its process the Heston model's, as QuantLib's (the S-hes plan's HR1 (ii)). An extension point:
/// <see cref="BatesDoubleExpDetJumpModel"/> derives from it.
/// </para>
/// <para>
/// QuantLib constrains η_u to be positive, yet for η_u ≥ 1 the up-jumps' exponential moment, which the model's drift
/// compensates, diverges: "the condition η1 &gt; 1 is necessary and sufficient for the random variable J_t to have
/// finite expectation" (Gulisashvili and Vives, on Kou's model, η1 = 1/η_u), and QuantLib's engine prices there, a
/// plausible 26.98 at η_u = 1.5. Here η_u's constraint is (0, 1), so the constructor refuses η_u ≥ 1 and a calibration
/// stays inside the model's domain, and <see cref="PricingEngines.BatesDoubleExpEngine"/> refuses a model at η_u ≥ 1
/// (the S-hes plan's HR7 (v), a refusal by the user's ruling, <c>Divergences/BatesDoubleExpUpJumpTests.cs</c>).
/// </para>
/// </remarks>
public class BatesDoubleExpModel : HestonModel
{
    /// <summary>Initializes a new instance of the <see cref="BatesDoubleExpModel"/> class.</summary>
    /// <param name="process">The process whose curves, spot and parameters the model starts from.</param>
    /// <param name="lambda">The jumps' intensity λ.</param>
    /// <param name="nuUp">The up-jumps' mean size η_u, in (0, 1).</param>
    /// <param name="nuDown">The down-jumps' mean size η_d.</param>
    /// <param name="p">The probability of an up-jump.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A parameter is outside its constraint.</exception>
    public BatesDoubleExpModel(
        HestonProcess process, double lambda = 0.1, double nuUp = 0.1, double nuDown = 0.1, double p = 0.5)
        : this(process, 9, lambda, nuUp, nuDown, p)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BatesDoubleExpModel"/> class with room for a derived model's
    /// arguments.
    /// </summary>
    /// <param name="process">The process whose curves, spot and parameters the model starts from.</param>
    /// <param name="nArguments">The number of arguments, at least 9.</param>
    /// <param name="lambda">The jumps' intensity λ.</param>
    /// <param name="nuUp">The up-jumps' mean size η_u, in (0, 1).</param>
    /// <param name="nuDown">The down-jumps' mean size η_d.</param>
    /// <param name="p">The probability of an up-jump.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="nArguments"/> is below 9.</exception>
    /// <exception cref="ArgumentException">A parameter is outside its constraint.</exception>
    protected BatesDoubleExpModel(
        HestonProcess process, int nArguments, double lambda, double nuUp, double nuDown, double p)
        : base(process, Checked(nArguments))
    {
        SetArgument(5, new ConstantParameter(p, new BoundaryConstraint(0.0, 1.0), nameof(p)));
        SetArgument(6, new ConstantParameter(nuDown, new PositiveConstraint(), nameof(nuDown)));
        SetArgument(7, new ConstantParameter(nuUp, new BatesDoubleExpUpJumpConstraint(), nameof(nuUp)));
        SetArgument(8, new ConstantParameter(lambda, new PositiveConstraint(), nameof(lambda)));
    }

    /// <summary>Gets the probability of an up-jump p.</summary>
    public double P => Arguments[5].Evaluate(0.0);

    /// <summary>Gets the down-jumps' mean size η_d.</summary>
    public double NuDown => Arguments[6].Evaluate(0.0);

    /// <summary>Gets the up-jumps' mean size η_u.</summary>
    public double NuUp => Arguments[7].Evaluate(0.0);

    /// <summary>Gets the jumps' intensity λ.</summary>
    public double Lambda => Arguments[8].Evaluate(0.0);

    private static int Checked(int nArguments)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(nArguments, 9);
        return nArguments;
    }
}
