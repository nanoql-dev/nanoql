// SPDX-FileCopyrightText: Copyright (C) 2018 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/schemes/trbdf2scheme.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>
/// A scheme that can step on an operator TR-BDF2 has already set over its whole step (the S-eq plan's SEQR9): nanoql's
/// one-step schemes, which <see cref="TrBDF2Scheme{TTrapezoidalScheme}"/> runs as its trapezoidal sub-step.
/// </summary>
/// <remarks>
/// Internal, so that <see cref="IFdmScheme"/> stays the whole concept a scheme of one's own implements: TR-BDF2 steps
/// such a scheme as it always does, setting its operator's time, which is QuantLib's TR-BDF2.
/// </remarks>
internal interface IFdmSetOperatorScheme
{
    /// <summary>
    /// Steps the values back from time <paramref name="t"/> on the operator as it is set, without setting its time; the
    /// boundary conditions keep the scheme's own times.
    /// </summary>
    /// <param name="a">The values.</param>
    /// <param name="t">The time the step starts from.</param>
    void StepOnSetOperator(Span<double> a, double t);
}
