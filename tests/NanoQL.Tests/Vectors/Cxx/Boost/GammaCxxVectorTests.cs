// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Boost's gamma family's harness vectors, <c>tests/vectors/cxx/boost/math/special_functions/gamma.json</c>:
/// <c>tgamma</c>, <c>lgamma</c>, <c>tgamma1pm1</c>, the incomplete gamma with its derivative, its two inverses, and
/// <c>tgamma_delta_ratio</c>, read through nanoql's internal <c>GammaFunctions</c> and <c>IgammaInverseFunctions</c>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GammaCxxVectorTests
{
    private static readonly Func<double, double> TGamma =
        BoostInternals.Function<Func<double, double>>("GammaFunctions", "TGamma");

    private static readonly Func<double, double> LGamma =
        BoostInternals.Function<Func<double, double>>("GammaFunctions", "LGamma");

    private static readonly Func<double, double> TGamma1pm1 =
        BoostInternals.Function<Func<double, double>>("GammaFunctions", "TGamma1pm1");

    private static readonly Func<double, double, double> GammaP =
        BoostInternals.Function<Func<double, double, double>>("GammaFunctions", "GammaP");

    private static readonly Func<double, double, double> GammaQ =
        BoostInternals.Function<Func<double, double, double>>("GammaFunctions", "GammaQ");

    private static readonly Func<double, double, double> GammaPDerivative =
        BoostInternals.Function<Func<double, double, double>>("GammaFunctions", "GammaPDerivative");

    private static readonly Func<double, double, double> GammaPInv =
        BoostInternals.Function<Func<double, double, double>>("IgammaInverseFunctions", "GammaPInv");

    private static readonly Func<double, double, double> GammaQInv =
        BoostInternals.Function<Func<double, double, double>>("IgammaInverseFunctions", "GammaQInv");

    private static readonly Func<double, double, double> TGammaDeltaRatio =
        BoostInternals.Function<Func<double, double, double>>("GammaFunctions", "TGammaDeltaRatio");

    [Fact]
    public void GammaCxxVector()
    {
        var file = VectorFile.Load("cxx/boost/math/special_functions/gamma");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.Name)
            {
                case "tgamma and lgamma":
                {
                    double[] z = Reals(c, "z");
                    check.Output(c, "tgamma", One(TGamma, z));
                    check.Output(c, "lgamma", One(LGamma, z));
                    break;
                }

                case "tgamma1pm1":
                    check.Output(c, "tgamma1pm1", One(TGamma1pm1, Reals(c, "z")));
                    break;

                case "the incomplete gamma":
                {
                    double[] a = Reals(c, "a"), x = Reals(c, "x");
                    check.Output(c, "gammaP", Two(GammaP, a, x));
                    check.Output(c, "gammaQ", Two(GammaQ, a, x));
                    check.Output(c, "gammaPDerivative", Two(GammaPDerivative, a, x));
                    break;
                }

                case "the inverses":
                {
                    double[] a = Reals(c, "a"), p = Reals(c, "p");
                    check.Output(c, "gammaPInv", Two(GammaPInv, a, p));
                    check.Output(c, "gammaQInv", Two(GammaQInv, a, p));
                    break;
                }

                case "tgamma_delta_ratio":
                    check.Output(c, "tgammaDeltaRatio", Two(TGammaDeltaRatio, Reals(c, "z"), Reals(c, "delta")));
                    break;

                default:
                    throw new InvalidOperationException($"unknown case {c.Name}");
            }
        }

        check.Complete();
    }

    private static double[] Reals(VectorCase c, string name) => [.. c.Input(name).EnumerateArray().Select(VectorCase.Real)];

    private static object?[] One(Func<double, double> f, double[] z) =>
        [.. z.Select(v => (object?)(Func<object?>)(() => f(v)))];

    private static object?[] Two(Func<double, double, double> f, double[] a, double[] x) =>
        [.. a.Zip(x, (s, t) => (object?)(Func<object?>)(() => f(s, t)))];
}
