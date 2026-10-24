// SPDX-FileCopyrightText: Copyright (C) 2022 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/qdfpamericanengine.hpp ql/pricingengines/vanilla/qdfpamericanengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.PricingEngines;

/// <summary>The iteration scheme of the fixed-point QD American engine.</summary>
/// <remarks>An extension point, as in QuantLib; the schemes are immutable.</remarks>
public abstract class QdFpIterationScheme
{
    /// <summary>The number of Chebyshev nodes interpolating the exercise boundary.</summary>
    /// <returns>The number of nodes.</returns>
    public abstract int GetNumberOfChebyshevInterpolationNodes();

    /// <summary>The number of naive (Richardson) fixed-point steps.</summary>
    /// <returns>The number of steps.</returns>
    public abstract int GetNumberOfNaiveFixedPointSteps();

    /// <summary>The number of partial Jacobi-Newton fixed-point steps.</summary>
    /// <returns>The number of steps.</returns>
    public abstract int GetNumberOfJacobiNewtonFixedPointSteps();

    /// <summary>The integrator within each fixed-point step.</summary>
    /// <returns>The integrator.</returns>
    public abstract Integrator GetFixedPointIntegrator();

    /// <summary>The integrator of the final conversion of the exercise boundary into the option's price.</summary>
    /// <returns>The integrator.</returns>
    public abstract Integrator GetExerciseBoundaryToPriceIntegrator();
}
