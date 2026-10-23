// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's GSR vectors, <c>tests/vectors/cxx/models/shortrate/onefactormodels/gsr.json</c>: the members of
/// <see cref="Gsr"/> and <see cref="Gaussian1dModel"/> the wheel doesn't expose: the numeraire time, the state's grid,
/// the calibration flags (the last index of each refused), and the two Gaussian polynomial integrals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GsrCxxVectorTests
{
    [Fact]
    public void GsrCxxVector()
    {
        var file = VectorFile.Load("cxx/models/shortrate/onefactormodels/gsr");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Inputs.TryGetProperty("integrals", out _))
            {
                check.Output(c, "gaussianPolynomialIntegral", ModelInputs.Points(c, "integrals")
                    .Select(p => (object?)Gaussian1dModel.GaussianPolynomialIntegral(p[0], p[1], p[2], p[3], p[4], p[5], p[6])).ToArray());
                check.Output(c, "gaussianShiftedPolynomialIntegral", ModelInputs.Points(c, "shifted")
                    .Select(p => (object?)Gaussian1dModel.GaussianShiftedPolynomialIntegral(p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7])).ToArray());
                continue;
            }

            Gsr model = Gaussian1dInputs.GsrOf(c.Input("model"));
            check.Output(c, "numeraireTime", model.NumeraireTime);
            check.Output(c, "yGrid", ModelInputs.Points(c, "yGrids")
                .Select(g => (Func<object?>)(() => ModelInputs.Elements(model.YGrid(g[0], (int)g[1], g[2], g[3], g[4])))));
            check.Output(c, "FixedReversions", Flags(model.FixedReversions()));
            check.Output(c, "FixedVolatilities", Flags(model.FixedVolatilities()));
            check.Output(c, "MoveVolatility", Enumerable.Range(0, model.Volatility.Length + 1)
                .Select(i => (Func<object?>)(() => Flags(model.MoveVolatility(i)))));
            check.Output(c, "MoveReversion", Enumerable.Range(0, model.Reversion.Length + 1)
                .Select(i => (Func<object?>)(() => Flags(model.MoveReversion(i)))));
        }

        check.Complete();
    }

    private static object?[] Flags(System.Collections.Immutable.ImmutableArray<bool> flags) => [.. flags.Select(f => (object?)f)];
}
