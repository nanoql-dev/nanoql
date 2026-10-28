// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Filon vectors, <c>tests/vectors/cxx/math/integrals/filonintegral.json</c>:
/// <see cref="FilonIntegral"/>'s sine and cosine integrals of four integrands where θ = t·h is at least 1.7 in size,
/// QuantLib's closed forms there, and an odd number of intervals, refused. The wheel doesn't expose it.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FilonIntegralCxxVectorTests
{
    // the harness's integrands, written as it writes them
    private static readonly Dictionary<string, Func<double, double>> Integrands = new()
    {
        ["1"] = _ => 1.0,
        ["exp(-x/2)"] = x => Math.Exp(-0.5 * x),
        ["x x"] = x => x * x,
        ["1/(1+x x)"] = x => 1.0 / (1.0 + (x * x)),
    };

    [Fact]
    public void FilonIntegralCxxVector()
    {
        var file = VectorFile.Load("cxx/math/integrals/filonintegral");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            FilonIntegralType type = Enum.Parse<FilonIntegralType>(c.String("type"));
            Func<double, double> f = Integrands[c.String("integrand")];
            check.Output(
                c, "integral", () => new FilonIntegral(type, c.Double("t"), c.Int("intervals")).Integrate(f, c.Double("a"), c.Double("b")));
        }

        check.Complete();
    }
}
