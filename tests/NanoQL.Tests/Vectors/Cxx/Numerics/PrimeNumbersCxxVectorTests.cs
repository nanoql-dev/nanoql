// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's prime-number vectors, <c>tests/vectors/cxx/math/primenumbers.json</c>:
/// <see cref="PrimeNumbers.Get"/>, which the wheel doesn't expose, at 500 indices up to 21,200.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PrimeNumbersCxxVectorTests
{
    [Fact]
    public void PrimeNumbersCxxVector()
    {
        var file = VectorFile.Load("cxx/math/primenumbers");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            int[] indices = c.Ints("absoluteIndex");
            check.Output(c, "get", indices.Select(i => (object?)(Func<object?>)(() => PrimeNumbers.Get(i))).ToArray());
        }

        check.Complete();
    }
}
