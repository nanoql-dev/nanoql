// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/multidimintegrator.hpp ql/experimental/math/multidimintegrator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>A scalar function of a vector, QuantLib's <c>std::function&lt;Real (const std::vector&lt;Real&gt;&amp;)&gt;</c>:
/// the integrand of a multidimensional integration.</summary>
/// <param name="x">The point, a view of the integrator's buffer valid during the call.</param>
/// <returns>The function's value.</returns>
[Experimental("NQL0001")]
public delegate double MultidimIntegrand(ReadOnlySpan<double> x);
