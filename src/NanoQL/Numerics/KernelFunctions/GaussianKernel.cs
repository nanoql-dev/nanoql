// SPDX-FileCopyrightText: Copyright (C) 2009 Dimitri Reiswich
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/kernelfunctions.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The Gaussian kernel: the normal density of the given average and standard deviation, times √(2π).
/// </summary>
public sealed class GaussianKernel : KernelFunction
{
    // √(2π), as M_SQRT2·M_SQRTPI
    private const double NormFact = MathConstants.M_SQRT2 * MathConstants.M_SQRTPI;

    private readonly NormalDistribution nd;
    private readonly CumulativeNormalDistribution cnd;

    /// <summary>Initializes a new instance of the <see cref="GaussianKernel"/> class.</summary>
    /// <param name="average">The average.</param>
    /// <param name="sigma">The standard deviation, positive.</param>
    /// <exception cref="ArgumentException"><paramref name="sigma"/> isn't positive.</exception>
    public GaussianKernel(double average, double sigma)
    {
        nd = new NormalDistribution(average, sigma);
        cnd = new CumulativeNormalDistribution(average, sigma);
    }

    /// <inheritdoc/>
    public override double Evaluate(double x) => nd.Evaluate(x) * NormFact;

    /// <summary>The kernel's derivative at <paramref name="x"/>.</summary>
    /// <param name="x">The argument.</param>
    /// <returns>The derivative.</returns>
    public double Derivative(double x) => nd.Derivative(x) * NormFact;

    /// <summary>The kernel's primitive at <paramref name="x"/>, from −∞.</summary>
    /// <param name="x">The argument.</param>
    /// <returns>The primitive.</returns>
    public double Primitive(double x) => cnd.Evaluate(x) * NormFact;
}
