// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/montecarlo/genericlsregression.hpp ql/methods/montecarlo/genericlsregression.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Methods;

/// <summary>
/// The results of a Longstaff-Schwartz regression: the biased estimate of the value it obtains while regressing, and
/// each exercise's basis coefficients, which QuantLib writes into the caller's vector.
/// </summary>
/// <param name="BiasedEstimate">The value estimated on the regression's own paths, biased high.</param>
/// <param name="BasisCoefficients">Each exercise's coefficients, one a basis function.</param>
public sealed record GenericLSRegressionResults(
    double BiasedEstimate, ImmutableArray<ImmutableArray<double>> BasisCoefficients);
