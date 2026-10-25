// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/equity/batesmodel.hpp ql/models/equity/batesmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.Models;

/// <summary>
/// The Heston model with double-exponential jumps at a deterministic intensity, mean-reverting at κ_λ to θ_λ.
/// </summary>
/// <remarks>
/// Its arguments are <see cref="BatesDoubleExpModel"/>'s nine, then κ_λ and θ_λ, both positive; its process the
/// Heston model's, as QuantLib's (the S-hes plan's HR1 (ii)); η_u in (0, 1) (HR7 (v)).
/// </remarks>
public sealed class BatesDoubleExpDetJumpModel : BatesDoubleExpModel
{
    /// <summary>Initializes a new instance of the <see cref="BatesDoubleExpDetJumpModel"/> class.</summary>
    /// <param name="process">The process whose curves, spot and parameters the model starts from.</param>
    /// <param name="lambda">The jumps' intensity λ.</param>
    /// <param name="nuUp">The up-jumps' mean size η_u, in (0, 1).</param>
    /// <param name="nuDown">The down-jumps' mean size η_d.</param>
    /// <param name="p">The probability of an up-jump.</param>
    /// <param name="kappaLambda">The intensity's mean-reversion speed κ_λ.</param>
    /// <param name="thetaLambda">The intensity's long-run mean θ_λ.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A parameter is outside its constraint.</exception>
    public BatesDoubleExpDetJumpModel(
        HestonProcess process,
        double lambda = 0.1,
        double nuUp = 0.1,
        double nuDown = 0.1,
        double p = 0.5,
        double kappaLambda = 1.0,
        double thetaLambda = 0.1)
        : base(process, 11, lambda, nuUp, nuDown, p)
    {
        SetArgument(9, new ConstantParameter(kappaLambda, new PositiveConstraint(), nameof(kappaLambda)));
        SetArgument(10, new ConstantParameter(thetaLambda, new PositiveConstraint(), nameof(thetaLambda)));
    }

    /// <summary>Gets the intensity's mean-reversion speed κ_λ.</summary>
    public double KappaLambda => Arguments[9].Evaluate(0.0);

    /// <summary>Gets the intensity's long-run mean θ_λ.</summary>
    public double ThetaLambda => Arguments[10].Evaluate(0.0);
}
