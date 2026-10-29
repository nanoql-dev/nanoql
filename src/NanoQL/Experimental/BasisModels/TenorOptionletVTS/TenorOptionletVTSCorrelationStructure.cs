// SPDX-FileCopyrightText: Copyright (C) 2018 Sebastian Schlenkrich
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/basismodels/tenoroptionletvts.hpp ql/experimental/basismodels/tenoroptionletvts.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>
/// The correlation between two forward rates by their start times, which <see cref="TenorOptionletVTS"/> reads:
/// QuantLib's <c>TenorOptionletVTS::CorrelationStructure</c>, by its typedef's name.
/// </summary>
/// <remarks>An extension point: <see cref="TwoParameterCorrelation"/> derives from it.</remarks>
[Experimental("NQL0001")]
public abstract class TenorOptionletVTSCorrelationStructure
{
    /// <summary>The correlation between the forward rates starting at two times: QuantLib's
    /// <c>operator()</c>.</summary>
    /// <param name="start1">The first rate's start time.</param>
    /// <param name="start2">The second rate's start time.</param>
    /// <returns>The correlation.</returns>
    public abstract double Evaluate(double start1, double start2);
}
