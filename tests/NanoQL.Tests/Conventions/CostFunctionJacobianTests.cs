// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="CostFunction.Jacobian"/>'s central differences hold the values at x + ε while they move x to x − ε.
/// QuantLib's <c>values</c> returns an <c>Array</c> by value, so what it holds is its own; a C# <c>Values</c> may return
/// its argument (QuantLib's <c>return x;</c>) or a buffer it reuses, so the default copies what it holds. The Jacobian
/// is then the same whichever <c>Values</c> returns.
/// </summary>
public class CostFunctionJacobianTests
{
    [Fact]
    public void AValuesReturningItsArgumentGivesTheSameJacobian()
    {
        var returnsItsArgument = new SimpleCostFunction(x => x);
        var returnsACopy = new SimpleCostFunction(x => x.Clone());
        Vector at = [1.0, 2.0];

        var jacobian = new Matrix(2, 2);
        var expected = new Matrix(2, 2);
        returnsItsArgument.Jacobian(jacobian, at);
        returnsACopy.Jacobian(expected, at);

        Assert.Equal(expected, jacobian);
        Assert.True(Math.Abs(jacobian[0, 0] - 1.0) < 1e-6 && Math.Abs(jacobian[1, 1] - 1.0) < 1e-6, jacobian.ToString());
        Assert.Equal([1.0, 2.0], at.AsSpan().ToArray());
    }
}
