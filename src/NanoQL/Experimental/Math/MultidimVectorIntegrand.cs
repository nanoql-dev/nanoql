// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/multidimquadrature.hpp ql/experimental/math/multidimquadrature.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>A vector function of a vector, QuantLib's
/// <c>std::function&lt;std::vector&lt;Real&gt; (const std::vector&lt;Real&gt;&amp;)&gt;</c>: the integrand of a
/// multidimensional vector integration.</summary>
/// <param name="x">The point, a view of the integrator's buffer valid during the call.</param>
/// <returns>The function's value, a fresh array the integrator may keep.</returns>
[Experimental("NQL0001")]
public delegate double[] MultidimVectorIntegrand(ReadOnlySpan<double> x);
