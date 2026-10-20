// SPDX-FileCopyrightText: Copyright (C) 2017 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrixutilities/gmres.hpp ql/math/matrixutilities/gmres.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Numerics;

/// <summary><see cref="GMRES"/>'s solution: the relative residual after each step, and the solution.</summary>
/// <param name="Errors">The residual's norm relative to the right-hand side's: the start's, then each step's.</param>
/// <param name="X">The solution.</param>
public readonly record struct GMRESResult(ImmutableArray<double> Errors, Vector X);
