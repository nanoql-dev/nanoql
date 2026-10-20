// SPDX-FileCopyrightText: Copyright (C) 2009 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2009 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrixutilities/bicgstab.hpp ql/math/matrixutilities/bicgstab.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary><see cref="BiCGstab"/>'s solution: the number of iterations, the relative residual and the
/// solution.</summary>
/// <param name="Iterations">The number of iterations.</param>
/// <param name="Error">The residual's norm relative to the right-hand side's.</param>
/// <param name="X">The solution.</param>
public readonly record struct BiCGStabResult(int Iterations, double Error, Vector X);
