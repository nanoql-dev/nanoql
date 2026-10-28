// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;


namespace NanoQL.Tests.Vectors;

/// <summary>
/// Boost's Student's t harness vectors, <c>tests/vectors/cxx/boost/math/distributions/students_t.json</c>: the
/// quantile at integer degrees of freedom, and the cumulative distribution and density, read through nanoql's internal
/// <c>StudentsTFunctions</c>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class StudentsTCxxVectorTests
{
    private static readonly Func<double, double, double> Quantile =
        BoostInternals.Function<Func<double, double, double>>("StudentsTFunctions", "Quantile");

    private static readonly Func<double, double, double> Cdf =
        BoostInternals.Function<Func<double, double, double>>("StudentsTFunctions", "Cdf");

    private static readonly Func<double, double, double> Pdf =
        BoostInternals.Function<Func<double, double, double>>("StudentsTFunctions", "Pdf");

    [Fact]
    public void StudentsTCxxVector()
    {
        var file = VectorFile.Load("cxx/boost/math/distributions/students_t");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] df = Reals(c, "df");
            switch (c.Name)
            {
                case "the quantile":
                    check.Output(c, "quantile", df.Zip(Reals(c, "p"), (d, q) => (object?)(Func<object?>)(() => Quantile(d, q))).ToArray());
                    break;
                case "the cdf":
                    check.Output(c, "cdf", df.Zip(Reals(c, "x"), (d, x) => (object?)(Func<object?>)(() => Cdf(d, x))).ToArray());
                    break;
                default:
                    check.Output(c, "pdf", df.Zip(Reals(c, "x"), (d, x) => (object?)(Func<object?>)(() => Pdf(d, x))).ToArray());
                    break;
            }
        }

        check.Complete();
    }

    private static double[] Reals(VectorCase c, string name) => [.. c.Input(name).EnumerateArray().Select(VectorCase.Real)];
}
