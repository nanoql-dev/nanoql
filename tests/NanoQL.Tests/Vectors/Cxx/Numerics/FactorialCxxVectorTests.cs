// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's factorial vectors, <c>tests/vectors/cxx/math/factorial.json</c>: <see cref="Factorial.Get"/>
/// and <see cref="Factorial.Ln"/>, which the wheel doesn't expose, from 0 to 300 and at four large n.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FactorialCxxVectorTests
{
    [Fact]
    public void FactorialCxxVector()
    {
        var file = VectorFile.Load("cxx/math/factorial");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            int[] ns = c.Ints("n");
            check.Output(c, "get", ns.Select(n => (object?)(Func<object?>)(() => Factorial.Get(n))).ToArray());
            check.Output(c, "ln", ns.Select(n => (object?)(Func<object?>)(() => Factorial.Ln(n))).ToArray());
        }

        check.Complete();
    }
}
