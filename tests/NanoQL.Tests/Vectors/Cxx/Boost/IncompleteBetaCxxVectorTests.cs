// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;


namespace NanoQL.Tests.Vectors;

/// <summary>
/// Boost's beta family's harness vectors, <c>tests/vectors/cxx/boost/math/special_functions/beta.json</c>:
/// <c>beta</c>, and the regularised incomplete beta <c>ibeta</c>, its complement <c>ibetac</c> and
/// <c>ibeta_derivative</c>, read through nanoql's internal <c>IncompleteBetaFunctions</c>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class IncompleteBetaCxxVectorTests
{
    private static readonly Func<double, double, double> Beta =
        BoostInternals.Function<Func<double, double, double>>("IncompleteBetaFunctions", "Beta");

    private static readonly Func<double, double, double, double> Ibeta =
        BoostInternals.Function<Func<double, double, double, double>>("IncompleteBetaFunctions", "Ibeta");

    private static readonly Func<double, double, double, double> Ibetac =
        BoostInternals.Function<Func<double, double, double, double>>("IncompleteBetaFunctions", "Ibetac");

    private static readonly Func<double, double, double, double> IbetaDerivative =
        BoostInternals.Function<Func<double, double, double, double>>("IncompleteBetaFunctions", "IbetaDerivative");

    [Fact]
    public void IncompleteBetaCxxVector()
    {
        var file = VectorFile.Load("cxx/boost/math/special_functions/beta");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] a = Reals(c, "a"), b = Reals(c, "b");
            if (c.Name == "beta")
            {
                check.Output(c, "beta", a.Zip(b, (s, t) => (object?)(Func<object?>)(() => Beta(s, t))).ToArray());
                continue;
            }

            double[] x = Reals(c, "x");
            check.Output(c, "ibeta", Three(Ibeta, a, b, x));
            check.Output(c, "ibetac", Three(Ibetac, a, b, x));
            check.Output(c, "ibetaDerivative", Three(IbetaDerivative, a, b, x));
        }

        check.Complete();
    }

    private static double[] Reals(VectorCase c, string name) => [.. c.Input(name).EnumerateArray().Select(VectorCase.Real)];

    private static object?[] Three(Func<double, double, double, double> f, double[] a, double[] b, double[] x) =>
        [.. Enumerable.Range(0, a.Length).Select(i => (object?)(Func<object?>)(() => f(a[i], b[i], x[i])))];
}
