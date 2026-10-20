// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Numerics;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The C runtime functions .NET lacks, written in the port that calls them (the playbook's row for
/// <c>std::log1p</c>): <c>std::expm1</c> and <c>std::log1p</c> in <see cref="Expm1Functions"/> return their argument
/// where e^x or 1 + x rounds to 1, the correctly rounded answer, as the C runtime's do. QuantLib's complex
/// <c>expm1</c> and <c>log1p</c> (<c>expm1.cpp</c>) reach them for |z| below 1 and for both parts below 0.5.
/// </summary>
public class CRuntimeFunctionTests
{
    [Fact]
    public void ExpM1AndLog1PReturnATinyArgument()
    {
        // e^z − 1 = z + z²/2 + …, and ln(1 + z) = z − z²/2 + …, where z² is 2e-40·i, below half an ulp of 1e-20
        var z = new Complex(1e-20, 1e-20);
        Assert.Equal(z, Expm1Functions.Expm1(z));
        Assert.Equal(z, Expm1Functions.Log1p(z));
    }
}
