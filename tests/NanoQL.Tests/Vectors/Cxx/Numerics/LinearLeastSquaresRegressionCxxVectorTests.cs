// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's regression vectors, <c>tests/vectors/cxx/math/linearleastsquaresregression.json</c>:
/// <see cref="LinearRegression"/> on one variable with three intercepts and on two and three variables, and
/// <see cref="LinearLeastSquaresRegression"/> on four sets of named basis functions, each regression's coefficients,
/// residuals, errors, dimension and size, and its standard errors where the basis has two functions (QuantLib's take
/// n − 2 degrees of freedom whatever the basis); and fewer samples than basis functions, refused. The wheel exposes
/// neither.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LinearLeastSquaresRegressionCxxVectorTests
{
    // the harness's basis functions, written as it writes them
    private static readonly Dictionary<string, Func<double, double>> Basis = new()
    {
        ["1"] = _ => 1.0,
        ["x"] = x => x,
        ["x x"] = x => x * x,
        ["sin(x)"] = Math.Sin,
        ["exp(-x)"] = x => Math.Exp(-x),
    };

    [Fact]
    public void LinearLeastSquaresRegressionCxxVector()
    {
        var file = VectorFile.Load("cxx/math/linearleastsquaresregression");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] y = c.Doubles("y");
            Func<GeneralLinearLeastSquares> regression = c.String("method") switch
            {
                "LinearRegression" => () => LinearRegression.Create(c.Doubles("x"), y, c.Double("intercept")),
                "LinearRegressionMulti" => () => LinearRegression.Create(
                    [.. c.Input("x").EnumerateArray().Select(r => new Vector(EquityInputs.Doubles(r)))], y, c.Double("intercept")),
                _ => () => LinearLeastSquaresRegression.Create(
                    c.Doubles("x"), y, [.. c.Input("basis").EnumerateArray().Select(b => Basis[b.GetString()!])]),
            };
            if (c.Expected.TryGetProperty("regression", out _))
            {
                check.Output(c, "regression", () => regression());
                continue;
            }

            GeneralLinearLeastSquares r = regression();
            check.Output(c, "coefficients", r.Coefficients().ToArray());
            check.Output(c, "residuals", r.Residuals().ToArray());
            if (c.Expected.TryGetProperty("standardErrors", out _))
            {
                check.Output(c, "standardErrors", r.StandardErrors().ToArray());
            }

            check.Output(c, "error", r.Error().ToArray());
            check.Output(c, "dim", r.Dim);
            check.Output(c, "size", r.Size);
        }

        check.Complete();
    }
}
