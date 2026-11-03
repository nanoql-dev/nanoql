// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2009 Frédéric Degraeve
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/linesearchbasedmethod.hpp ql/math/optimization/linesearchbasedmethod.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// A line-search method's state through one minimization, which QuantLib's methods keep as members across
/// minimizations (BFGS's inverse Hessian).
/// </summary>
/// <remarks>
/// A working type: a <see cref="LineSearchBasedMethod"/> creates one at each minimization's start
/// (<see cref="LineSearchBasedMethod"/>'s <c>NewMinimizationState</c>) and changes it as the minimization runs, so the
/// method itself stays immutable and each minimization starts afresh. An extension point: a method of one's own
/// derives its state from it.
/// </remarks>
[WorkingType]
public abstract class LineSearchBasedMethodState
{
}
