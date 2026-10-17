// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A <c>QL_FAIL</c> throws <see cref="NanoQLException"/> with QuantLib's message (playbook §2's error rows), also where
/// it sits in a branch on an argument: only an enum argument a member always refuses throws
/// <see cref="ArgumentOutOfRangeException"/>. A vector's <c>{"throws": true}</c> accepts either type, so this fact
/// asserts them.
/// </summary>
public class QuantLibFailTests
{
    [Fact]
    public void AFailOnAnArgumentThrowsNanoQLException()
    {
        // normaldistribution.cpp:99-100, the tail beyond rounding of 0 and 1.
        NanoQLException tail = Assert.Throws<NanoQLException>(() => InverseCumulativeNormal.StandardValue(1.5));
        Assert.Equal("InverseCumulativeNormal(1.5) undefined: must be 0 < x < 1", tail.Message);

        // tridiagonaloperator.cpp:38-41, a size neither null nor at least 2.
        NanoQLException size = Assert.Throws<NanoQLException>(() => new TridiagonalOperator(1));
        Assert.Equal("invalid size (1) for tridiagonal operator (must be null or >= 2)", size.Message);
    }
}
