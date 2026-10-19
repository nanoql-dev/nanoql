// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Mario Pucci
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/xabrinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Numerics;

/// <summary>
/// A fitted XABR interpolation's results: the four SABR-style parameters every XABR model has, the fit's errors, the
/// strikes' weights and the end criteria of the minimization kept. The view a swaption volatility cube reads.
/// </summary>
/// <remarks>
/// An extension point: <see cref="SABRInterpolation"/> implements it, and a model's interpolation of one's own does, to
/// be fitted by a cube on that model.
/// </remarks>
public interface IXabrInterpolation
{
    /// <summary>Gets the fitted α.</summary>
    double Alpha { get; }

    /// <summary>Gets the fitted β.</summary>
    double Beta { get; }

    /// <summary>Gets the fitted ν.</summary>
    double Nu { get; }

    /// <summary>Gets the fitted ρ.</summary>
    double Rho { get; }

    /// <summary>Gets the root-mean-square error of the fit, weighted by <see cref="InterpolationWeights"/>.</summary>
    double RmsError { get; }

    /// <summary>Gets the largest error of the fit, unweighted.</summary>
    double MaxError { get; }

    /// <summary>Gets the end criteria of the minimization the fit kept; <see cref="EndCriteriaType.None"/> where it ran none.</summary>
    EndCriteriaType EndCriteria { get; }

    /// <summary>The strikes' weights in the fit's errors, normalized.</summary>
    /// <returns>The weights, one per strike.</returns>
    ImmutableArray<double> InterpolationWeights();
}
