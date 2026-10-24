// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Boost's non-central chi-squared distribution's harness vectors,
/// <c>tests/vectors/cxx/boost/math/distributions/non_central_chi_squared.json</c>: the pdf, the cdf and its complement,
/// the quantile and its complement, and the refused arguments, read through nanoql's internal
/// <c>NonCentralChiSquaredFunctions</c>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class NonCentralChiSquaredCxxVectorTests
{
    private static readonly Func<double, double, double, double> Pdf = Function("Pdf");
    private static readonly Func<double, double, double, double> Cdf = Function("Cdf");
    private static readonly Func<double, double, double, double> CdfComplement = Function("CdfComplement");
    private static readonly Func<double, double, double, double> Quantile = Function("Quantile");
    private static readonly Func<double, double, double, double> QuantileComplement = Function("QuantileComplement");

    [Fact]
    public void NonCentralChiSquaredCxxVector()
    {
        var file = VectorFile.Load("cxx/boost/math/distributions/non_central_chi_squared");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] k = Reals(c, "k"), l = Reals(c, "l");
            switch (c.Name)
            {
                case "the pdf and cdf":
                {
                    double[] x = Reals(c, "x");
                    check.Output(c, "pdf", Outputs(Pdf, k, l, x));
                    check.Output(c, "cdf", Outputs(Cdf, k, l, x));
                    check.Output(c, "cdfComplement", Outputs(CdfComplement, k, l, x));
                    break;
                }

                case "the quantile":
                {
                    double[] p = Reals(c, "p");
                    check.Output(c, "quantile", Outputs(Quantile, k, l, p));
                    check.Output(c, "quantileComplement", Outputs(QuantileComplement, k, l, p));
                    break;
                }

                default:
                    throw new InvalidOperationException($"unknown case {c.Name}");
            }
        }

        check.Complete();
    }

    private static Func<double, double, double, double> Function(string name) =>
        BoostInternals.Function<Func<double, double, double, double>>("NonCentralChiSquaredFunctions", name);

    private static double[] Reals(VectorCase c, string name) => [.. c.Input(name).EnumerateArray().Select(VectorCase.Real)];

    private static object?[] Outputs(Func<double, double, double, double> f, double[] k, double[] l, double[] x) =>
        [.. Enumerable.Range(0, k.Length).Select(i => (object?)(Func<object?>)(() => f(k[i], l[i], x[i])))];
}
