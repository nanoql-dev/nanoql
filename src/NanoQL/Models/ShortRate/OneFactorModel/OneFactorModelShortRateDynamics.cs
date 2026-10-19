// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/shortrate/onefactormodel.hpp ql/models/shortrate/onefactormodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>The base class describing a one-factor model's short-rate dynamics: the short rate and its state variable.</summary>
/// <remarks>QuantLib's <c>OneFactorModel::ShortRateDynamics</c>. An extension point, as in QuantLib: each model's dynamics derive from it.</remarks>
public abstract class OneFactorModelShortRateDynamics
{
    /// <summary>Initializes a new instance of the <see cref="OneFactorModelShortRateDynamics"/> class.</summary>
    /// <param name="process">The risk-neutral dynamics of the state variable.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is null.</exception>
    protected OneFactorModelShortRateDynamics(StochasticProcess1D process)
    {
        ArgumentNullException.ThrowIfNull(process);
        Process = process;
    }

    /// <summary>Gets the risk-neutral dynamics of the state variable.</summary>
    public StochasticProcess1D Process { get; }

    /// <summary>Computes the state variable from the short rate.</summary>
    /// <param name="t">The time.</param>
    /// <param name="r">The short rate.</param>
    /// <returns>The state variable.</returns>
    public abstract double Variable(double t, double r);

    /// <summary>Computes the short rate from the state variable.</summary>
    /// <param name="t">The time.</param>
    /// <param name="variable">The state variable.</param>
    /// <returns>The short rate.</returns>
    public abstract double ShortRate(double t, double variable);
}
