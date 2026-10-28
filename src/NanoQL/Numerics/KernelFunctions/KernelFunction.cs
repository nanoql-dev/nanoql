// SPDX-FileCopyrightText: Copyright (C) 2009 Dimitri Reiswich
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/kernelfunctions.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// A kernel function in the statistical sense: a nonnegative, real-valued function which integrates to one and is
/// symmetric. An extension point.
/// </summary>
public abstract class KernelFunction
{
    /// <summary>The kernel's value at <paramref name="x"/>.</summary>
    /// <param name="x">The argument.</param>
    /// <returns>The value.</returns>
    public abstract double Evaluate(double x);
}
