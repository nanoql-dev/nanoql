// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/stepcondition.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: NullCondition, which no QuantLib 1.44 class or test uses and which waits for its first user

namespace NanoQL.Methods;

/// <summary>A condition applied to a finite-difference problem's values at every time step.</summary>
/// <remarks>
/// <para>
/// A working type: a condition is owned by one computation, and may hold the state it reads back (a snapshot) or
/// buffers it works in. <see cref="ApplyTo"/> modifies the values in place, through the caller's span. QuantLib's
/// template parameter is the array type; every finite-difference array here is a span of doubles, so the class isn't
/// generic.
/// </para>
/// <para>An extension point, as in QuantLib: later units derive their step conditions from it.</para>
/// </remarks>
[WorkingType]
public abstract class StepCondition
{
    /// <summary>Applies the condition to the values at time <paramref name="t"/>, in place.</summary>
    /// <param name="a">The values.</param>
    /// <param name="t">The time.</param>
    public abstract void ApplyTo(Span<double> a, double t);
}
