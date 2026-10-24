// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Boost's chi-squared distribution's harness vectors, <c>tests/vectors/cxx/boost/math/distributions/chi_squared.json</c>:
/// the quantile and its complement, the pdf, the cdf and its complement, and the refused arguments, read through
/// nanoql's internal <c>ChiSquaredFunctions</c>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ChiSquaredCxxVectorTests
{
    private static readonly Func<double, double, double> Quantile = Function("Quantile");
    private static readonly Func<double, double, double> QuantileComplement = Function("QuantileComplement");
    private static readonly Func<double, double, double> Pdf = Function("Pdf");
    private static readonly Func<double, double, double> Cdf = Function("Cdf");
    private static readonly Func<double, double, double> CdfComplement = Function("CdfComplement");

    [Fact]
    public void ChiSquaredCxxVector()
    {
        var file = VectorFile.Load("cxx/boost/math/distributions/chi_squared");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] df = Reals(c, "df");
            switch (c.Name)
            {
                case "the quantile":
                {
                    double[] p = Reals(c, "p");
                    check.Output(c, "quantile", Outputs(Quantile, df, p));
                    check.Output(c, "quantileComplement", Outputs(QuantileComplement, df, p));
                    break;
                }

                case "the pdf and cdf":
                {
                    double[] x = Reals(c, "x");
                    check.Output(c, "pdf", Outputs(Pdf, df, x));
                    check.Output(c, "cdf", Outputs(Cdf, df, x));
                    check.Output(c, "cdfComplement", Outputs(CdfComplement, df, x));
                    break;
                }

                default:
                    throw new InvalidOperationException($"unknown case {c.Name}");
            }
        }

        check.Complete();
    }

    private static Func<double, double, double> Function(string name) =>
        BoostInternals.Function<Func<double, double, double>>("ChiSquaredFunctions", name);

    private static double[] Reals(VectorCase c, string name) => [.. c.Input(name).EnumerateArray().Select(VectorCase.Real)];

    private static object?[] Outputs(Func<double, double, double> f, double[] a, double[] b) =>
        [.. a.Zip(b, (s, t) => (object?)(Func<object?>)(() => f(s, t)))];
}
