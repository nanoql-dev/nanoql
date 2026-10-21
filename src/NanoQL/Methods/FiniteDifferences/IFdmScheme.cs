// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/finitedifferencemodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>
/// A finite-difference time-stepping scheme: the evolver concept QuantLib's <c>FiniteDifferenceModel</c> template
/// reads, which steps the values back over a set step.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's schemes share no base: the model's template takes any class with <c>step</c> and <c>setStep</c>. Here the
/// model's type parameter is constrained to this interface (D12), and every QuantLib scheme implements it. A scheme is
/// a working type (the S-eq plan's SEQR1): <see cref="Step"/> modifies the values in place, through scratch buffers
/// the scheme holds.
/// </para>
/// <para>An extension point: a later unit's scheme implements it.</para>
/// </remarks>
public interface IFdmScheme
{
    /// <summary>Steps the values back from time <paramref name="t"/> by the set step, in place.</summary>
    /// <param name="a">The values.</param>
    /// <param name="t">The time the step starts from.</param>
    void Step(Span<double> a, double t);

    /// <summary>Sets the step's length.</summary>
    /// <param name="dt">The length.</param>
    void SetStep(double dt);
}
