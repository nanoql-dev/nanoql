// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/processes/geometricbrownianprocess.hpp ql/processes/geometricbrownianprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Processes;

/// <summary>The geometric Brownian-motion process, governed by dS(t, S) = μS dt + σS dW_t.</summary>
/// <remarks>Discretized by Euler's scheme, as QuantLib's.</remarks>
public sealed class GeometricBrownianMotionProcess : StochasticProcess1D
{
    private readonly double initialValue;
    private readonly double mue;
    private readonly double sigma;

    /// <summary>Initializes a new instance of the <see cref="GeometricBrownianMotionProcess"/> class.</summary>
    /// <param name="initialValue">The initial value, S₀.</param>
    /// <param name="mue">The drift rate, μ.</param>
    /// <param name="sigma">The volatility, σ.</param>
    public GeometricBrownianMotionProcess(double initialValue, double mue, double sigma)
        : base(new EulerDiscretization())
    {
        this.initialValue = initialValue;
        this.mue = mue;
        this.sigma = sigma;
    }

    /// <inheritdoc/>
    public override double X0 => initialValue;

    /// <inheritdoc/>
    public override double Drift(double t, double x) => mue * x;

    /// <inheritdoc/>
    public override double Diffusion(double t, double x) => sigma * x;
}
