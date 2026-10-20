// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/integral.hpp ql/math/integrals/integral.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// An integration's value and the number of function evaluations it made, which QuantLib's integrator keeps in a
/// mutable member and returns by <c>numberOfEvaluations()</c> (<c>integral.hpp:54</c>).
/// </summary>
/// <param name="Value">The integral.</param>
/// <param name="NumberOfEvaluations">The number of evaluations of the integrand.</param>
public readonly record struct IntegrationResult(double Value, int NumberOfEvaluations);
