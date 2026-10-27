// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Nicolas Di Césaré
// SPDX-FileCopyrightText: Copyright (C) 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/leastsquare.hpp ql/math/optimization/leastsquare.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Numerics;

/// <summary>The results of a non-linear least-squares fit, QuantLib's <c>NonLinearLeastSquare</c> inspectors.</summary>
/// <param name="Results">The point found.</param>
/// <param name="ResidualNorm">The cost there, the square of the residuals' norm.</param>
/// <param name="LastValue">The cost there, QuantLib's <c>lastValue()</c>.</param>
/// <param name="ExitFlag">The end criterion the minimization met.</param>
public sealed record NonLinearLeastSquareResults(
    ImmutableArray<double> Results, double ResidualNorm, double LastValue, EndCriteriaType ExitFlag);
