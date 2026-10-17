// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/stochasticprocess.hpp ql/stochasticprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL;

/// <summary>
/// The discretization of a <see cref="StochasticProcess"/> over a time interval: QuantLib's nested
/// <c>StochasticProcess::discretization</c>.
/// </summary>
/// <remarks>An extension point: the discretizations derive from it.</remarks>
public abstract class StochasticProcessDiscretization
{
    /// <summary>The drift over the interval.</summary>
    /// <param name="process">The process.</param>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>A new vector.</returns>
    public abstract Vector Drift(StochasticProcess process, double t0, Vector x0, double dt);

    /// <summary>The diffusion over the interval.</summary>
    /// <param name="process">The process.</param>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>A new matrix.</returns>
    public abstract Matrix Diffusion(StochasticProcess process, double t0, Vector x0, double dt);

    /// <summary>The covariance over the interval.</summary>
    /// <param name="process">The process.</param>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>A new matrix.</returns>
    public abstract Matrix Covariance(StochasticProcess process, double t0, Vector x0, double dt);
}
