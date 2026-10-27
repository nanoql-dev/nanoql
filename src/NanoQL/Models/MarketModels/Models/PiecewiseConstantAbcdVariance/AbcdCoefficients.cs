// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/models/piecewiseconstantabcdvariance.hpp ql/models/marketmodels/models/piecewiseconstantabcdvariance.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>
/// An abcd function's four parameters, <see cref="PiecewiseConstantAbcdVariance.GetABCD"/>'s result: QuantLib's
/// <c>getABCD</c> out-parameters.
/// </summary>
/// <param name="A">The abcd function's a.</param>
/// <param name="B">The abcd function's b.</param>
/// <param name="C">The abcd function's c.</param>
/// <param name="D">The abcd function's d.</param>
public readonly record struct AbcdCoefficients(double A, double B, double C, double D);
