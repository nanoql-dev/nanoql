// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/stochasticprocess.hpp ql/stochasticprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL;

/// <summary>
/// The discretization of a <see cref="StochasticProcess1D"/> over a time interval: QuantLib's nested
/// <c>StochasticProcess1D::discretization</c>.
/// </summary>
/// <remarks>An extension point: the 1-D discretizations derive from it.</remarks>
public abstract class StochasticProcess1DDiscretization
{
    /// <summary>The drift over the interval.</summary>
    /// <param name="process">The process.</param>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>The drift.</returns>
    public abstract double Drift(StochasticProcess1D process, double t0, double x0, double dt);

    /// <summary>The diffusion over the interval.</summary>
    /// <param name="process">The process.</param>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>The diffusion.</returns>
    public abstract double Diffusion(StochasticProcess1D process, double t0, double x0, double dt);

    /// <summary>The variance over the interval.</summary>
    /// <param name="process">The process.</param>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>The variance.</returns>
    public abstract double Variance(StochasticProcess1D process, double t0, double x0, double dt);
}
