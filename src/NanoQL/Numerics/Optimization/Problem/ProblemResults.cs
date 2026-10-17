// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Nicolas Di Césaré
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/problem.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Equality-tested: tests/NanoQL.Tests/Conventions/ProblemResultsEqualityTests.cs

using System.Collections.Immutable;

namespace NanoQL.Numerics;

/// <summary>
/// The results of a minimization: what QuantLib's <c>minimize</c> leaves in the <c>Problem</c> it changes, and the
/// end criterion it returns.
/// </summary>
/// <param name="CurrentValue">The current value of the local minimum, the minimization's result.</param>
/// <param name="FunctionValue">
/// The value of the cost function at the current value; <see langword="null"/> (QuantLib's <c>Null&lt;Real&gt;()</c>)
/// where the method sets none.
/// </param>
/// <param name="GradientNormValue">
/// The value of the gradient norm at the current value; <see langword="null"/> (QuantLib's <c>Null&lt;Real&gt;()</c>)
/// where the method sets none.
/// </param>
/// <param name="FunctionEvaluation">The number of evaluations of the cost function.</param>
/// <param name="GradientEvaluation">The number of evaluations of the cost function's gradient.</param>
/// <param name="EndCriteriaType">Why the minimization ended.</param>
/// <remarks>Equality compares every member by value, the current value element by element.</remarks>
public sealed record ProblemResults(
    ImmutableArray<double> CurrentValue,
    double? FunctionValue,
    double? GradientNormValue,
    int FunctionEvaluation,
    int GradientEvaluation,
    EndCriteriaType EndCriteriaType)
{
    /// <inheritdoc/>
    public bool Equals(ProblemResults? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is not null
            && CurrentValue.AsSpan().SequenceEqual(other.CurrentValue.AsSpan())
            && EqualityComparer<double?>.Default.Equals(FunctionValue, other.FunctionValue)
            && EqualityComparer<double?>.Default.Equals(GradientNormValue, other.GradientNormValue)
            && FunctionEvaluation == other.FunctionEvaluation
            && GradientEvaluation == other.GradientEvaluation
            && EndCriteriaType == other.EndCriteriaType;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        foreach (double x in CurrentValue.AsSpan())
        {
            hash.Add(x);
        }

        hash.Add(FunctionValue);
        hash.Add(GradientNormValue);
        hash.Add(FunctionEvaluation);
        hash.Add(GradientEvaluation);
        hash.Add(EndCriteriaType);
        return hash.ToHashCode();
    }
}
