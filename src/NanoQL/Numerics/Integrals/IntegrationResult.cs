// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/integral.hpp ql/math/integrals/integral.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// An integration's value, the number of function evaluations it made and its error estimate, which QuantLib's
/// integrator keeps in mutable members and returns by <c>numberOfEvaluations()</c> and <c>absoluteError()</c>
/// (<c>integral.hpp:52-54</c>).
/// </summary>
/// <param name="Value">The integral.</param>
/// <param name="NumberOfEvaluations">The number of evaluations of the integrand.</param>
public readonly record struct IntegrationResult(double Value, int NumberOfEvaluations)
{
    /// <summary>
    /// Gets the integrator's estimate of the absolute error: <see langword="null"/> where QuantLib's integrator
    /// computes none, so its <c>absoluteError()</c> reads an unset member; 0 for an empty interval, whose integral is
    /// exact.
    /// </summary>
    /// <remarks>
    /// QuantLib sets the error in three integrators only: the non-adaptive Gauss-Kronrod rule
    /// (<c>kronrodintegral.cpp:300</c>, <c>:326</c>, <c>:348</c>), tanh-sinh and exp-sinh; its one reader in QuantLib's
    /// library is <see cref="Integrator.IntegrationSuccess"/>.
    /// </remarks>
    public double? AbsoluteError { get; init; }
}
