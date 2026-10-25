// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/equity/batesmodel.hpp ql/models/equity/batesmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.Models;

/// <summary>
/// The Bates model with a deterministic jump intensity, mean-reverting at κ_λ to θ_λ: dλ(t) = κ_λ(θ_λ − λ) dt.
/// </summary>
/// <remarks>
/// Its arguments are <see cref="BatesModel"/>'s eight, then κ_λ and θ_λ, both positive; its process the Bates
/// model's, as QuantLib's (the S-hes plan's HR1 (ii)).
/// </remarks>
public sealed class BatesDetJumpModel : BatesModel
{
    /// <summary>Initializes a new instance of the <see cref="BatesDetJumpModel"/> class.</summary>
    /// <param name="process">The process whose curves, spot and parameters the model starts from.</param>
    /// <param name="kappaLambda">The intensity's mean-reversion speed κ_λ.</param>
    /// <param name="thetaLambda">The intensity's long-run mean θ_λ.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A parameter is outside its constraint.</exception>
    public BatesDetJumpModel(BatesProcess process, double kappaLambda = 1.0, double thetaLambda = 0.1)
        : base(process, 10)
    {
        SetArgument(8, new ConstantParameter(kappaLambda, new PositiveConstraint(), nameof(kappaLambda)));
        SetArgument(9, new ConstantParameter(thetaLambda, new PositiveConstraint(), nameof(thetaLambda)));
    }

    /// <summary>Gets the intensity's mean-reversion speed κ_λ.</summary>
    public double KappaLambda => Arguments[8].Evaluate(0.0);

    /// <summary>Gets the intensity's long-run mean θ_λ.</summary>
    public double ThetaLambda => Arguments[9].Evaluate(0.0);
}
