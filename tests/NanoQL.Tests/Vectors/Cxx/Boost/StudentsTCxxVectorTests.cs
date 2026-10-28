// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;


namespace NanoQL.Tests.Vectors;

/// <summary>
/// Boost's Student's t quantile's harness vectors, <c>tests/vectors/cxx/boost/math/distributions/students_t.json</c>:
/// the quantile at integer degrees of freedom, read through nanoql's internal <c>StudentsTFunctions</c>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class StudentsTCxxVectorTests
{
    private static readonly Func<double, double, double> Quantile =
        BoostInternals.Function<Func<double, double, double>>("StudentsTFunctions", "Quantile");

    [Fact]
    public void StudentsTCxxVector()
    {
        var file = VectorFile.Load("cxx/boost/math/distributions/students_t");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] df = Reals(c, "df"), p = Reals(c, "p");
            check.Output(c, "quantile", df.Zip(p, (d, q) => (object?)(Func<object?>)(() => Quantile(d, q))).ToArray());
        }

        check.Complete();
    }

    private static double[] Reals(VectorCase c, string name) => [.. c.Input(name).EnumerateArray().Select(VectorCase.Real)];
}
