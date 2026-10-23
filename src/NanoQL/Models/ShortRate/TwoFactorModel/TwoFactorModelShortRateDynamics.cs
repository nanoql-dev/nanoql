// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/shortrate/twofactormodel.hpp ql/models/shortrate/twofactormodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.Models;

/// <summary>
/// The class describing the dynamics of a two-factor model's state variables: the short rate rₜ = f(t, xₜ, yₜ), with
/// dxₜ = μₓ(t, xₜ)dt + σₓ(t, xₜ)dWˣₜ, dyₜ = μᵧ(t, yₜ)dt + σᵧ(t, yₜ)dWʸₜ and dWˣₜdWʸₜ = ρ dt.
/// </summary>
/// <remarks>QuantLib's <c>TwoFactorModel::ShortRateDynamics</c>. An extension point, as in QuantLib: each model's dynamics derive from it.</remarks>
public abstract class TwoFactorModelShortRateDynamics
{
    /// <summary>Initializes a new instance of the <see cref="TwoFactorModelShortRateDynamics"/> class.</summary>
    /// <param name="xProcess">The risk-neutral dynamics of the first state variable.</param>
    /// <param name="yProcess">The risk-neutral dynamics of the second state variable.</param>
    /// <param name="correlation">The correlation ρ between the two Brownian motions.</param>
    /// <exception cref="ArgumentNullException"><paramref name="xProcess"/> or <paramref name="yProcess"/> is null.</exception>
    protected TwoFactorModelShortRateDynamics(StochasticProcess1D xProcess, StochasticProcess1D yProcess, double correlation)
    {
        ArgumentNullException.ThrowIfNull(xProcess);
        ArgumentNullException.ThrowIfNull(yProcess);
        XProcess = xProcess;
        YProcess = yProcess;
        Correlation = correlation;
    }

    /// <summary>Gets the risk-neutral dynamics of the first state variable x.</summary>
    public StochasticProcess1D XProcess { get; }

    /// <summary>Gets the risk-neutral dynamics of the second state variable y.</summary>
    public StochasticProcess1D YProcess { get; }

    /// <summary>Gets the correlation ρ between the two Brownian motions.</summary>
    public double Correlation { get; }

    /// <summary>Computes the short rate from the state variables.</summary>
    /// <param name="t">The time.</param>
    /// <param name="x">The first state variable.</param>
    /// <param name="y">The second state variable.</param>
    /// <returns>The short rate.</returns>
    public abstract double ShortRate(double t, double x, double y);

    /// <summary>The joint process of the two variables, correlated.</summary>
    /// <returns>A new process array.</returns>
    public StochasticProcess Process()
    {
        var correlation = new Matrix(2, 2);
        correlation[0, 0] = correlation[1, 1] = 1.0;
        correlation[0, 1] = correlation[1, 0] = Correlation;
        return new StochasticProcessArray(ImmutableArray.Create(XProcess, YProcess), correlation);
    }
}
