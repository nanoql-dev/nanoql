// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A tridiagonal operator of size 1 can be built (QuantLib's <c>identity(1)</c>, or three diagonals of sizes 0, 1 and
/// 0), but QuantLib's <c>applyTo</c>, <c>solveFor</c> and <c>SOR</c> then read past its arrays
/// (<c>tridiagonaloperator.cpp:70-74,106-108,131-133</c>; <c>solveFor</c>'s <c>n_-2</c> wraps), a result C++ leaves
/// undefined. nanoql refuses the three with <see cref="NanoQLException"/>.
/// </summary>
public class TridiagonalOperatorSizeOneTests
{
    [Fact]
    public void ApplyingOrSolvingASizeOneOperatorIsRefused()
    {
        TridiagonalOperator one = TridiagonalOperator.Identity(1);
        var rhs = new Vector(1, 2.0);

        Assert.Equal(1, one.Size);
        Assert.Contains("applyTo", Assert.Throws<NanoQLException>(() => one.ApplyTo(rhs)).Message, StringComparison.Ordinal);
        Assert.Contains("solveFor", Assert.Throws<NanoQLException>(() => one.SolveFor(rhs)).Message, StringComparison.Ordinal);
        Assert.Contains(
            "solveFor",
            Assert.Throws<NanoQLException>(() => one.SolveFor(rhs, new Vector(1))).Message,
            StringComparison.Ordinal);
        Assert.Contains("SOR", Assert.Throws<NanoQLException>(() => one.SOR(rhs, 1e-8)).Message, StringComparison.Ordinal);
    }
}
