// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Boost's modified Bessel function's harness vectors, <c>tests/vectors/cxx/boost/math/special_functions/bessel.json</c>:
/// <c>cyl_bessel_i</c> over orders and arguments that reach each of its methods, read through nanoql's internal
/// <c>BesselFunctions</c>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BesselCxxVectorTests
{
    private static readonly Func<double, double, double> CylBesselI =
        BoostInternals.Function<Func<double, double, double>>("BesselFunctions", "CylBesselI");

    [Fact]
    public void BesselCxxVector()
    {
        var file = VectorFile.Load("cxx/boost/math/special_functions/bessel");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] v = Reals(c, "v"), x = Reals(c, "x");
            check.Output(c, "cylBesselI", (object?[])[.. v.Zip(x, (s, t) => (object?)(Func<object?>)(() => CylBesselI(s, t)))]);
        }

        check.Complete();
    }

    private static double[] Reals(VectorCase c, string name) => [.. c.Input(name).EnumerateArray().Select(VectorCase.Real)];
}
