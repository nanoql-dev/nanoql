// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="ProblemResults"/> compares its current value element by element (docs/porting-playbook.md §4), where a
/// record's own equality would compare the <see cref="ImmutableArray{T}"/> by reference; QuantLib's <c>Problem</c> has
/// no <c>operator==</c>.
/// </summary>
public class ProblemResultsEqualityTests
{
    [Fact]
    public void EqualityComparesTheCurrentValueByContent()
    {
        ProblemResults a = Results([1.0, 2.0]), b = Results([1.0, 2.0]);
        Assert.NotSame(a, b);
        Assert.True(a.Equals(b));
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.True(a.Equals((object)b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.True(a.Equals(a));
        Assert.False(a.Equals(null));

        // Every member counts.
        Assert.NotEqual(a, Results([1.0, 2.5]));
        Assert.NotEqual(a, Results([1.0]));
        Assert.NotEqual(a, a with { FunctionValue = 0.5 });
        Assert.NotEqual(a, a with { FunctionValue = null });
        Assert.NotEqual(a, a with { GradientNormValue = 0.25 });
        Assert.NotEqual(a, a with { FunctionEvaluation = 8 });
        Assert.NotEqual(a, a with { GradientEvaluation = 1 });
        Assert.NotEqual(a, a with { EndCriteriaType = EndCriteriaType.MaxIterations });

        // A NaN element equals itself, as double.Equals has it, so a result equals a copy of itself.
        Assert.Equal(Results([double.NaN]), Results([double.NaN]));
    }

    private static ProblemResults Results(double[] currentValue) =>
        new([.. currentValue], 0.125, null, 7, 0, EndCriteriaType.StationaryPoint);
}
