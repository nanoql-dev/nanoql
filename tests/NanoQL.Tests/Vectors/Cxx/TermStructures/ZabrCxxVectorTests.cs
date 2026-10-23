// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's ZABR model vectors, <c>tests/vectors/cxx/termstructures/volatility/zabr.json</c>:
/// <see cref="ZabrModel"/>'s inspectors, its lognormal, normal and local volatilities and its call prices by finite
/// differences on the Dupire equation in their vector and scalar forms, for models with γ at and away from 1; and
/// QuantLib's refusals of the model's arguments and of the vector members' strikes.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ZabrCxxVectorTests
{
    [Fact]
    public void ZabrCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/volatility/zabr");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("model");
            switch (c.String("kind"))
            {
                case "model":
                    CheckModel(check, c, ModelOf(spec));
                    break;
                case "refusal":
                    check.Output(c, "model", () => ModelOf(spec).Forward);
                    break;
                case "strikes":
                    ZabrModel model = ModelOf(spec);
                    double[] strikes = Numbers(c.Input("strikes"));
                    check.Output(c, "lognormalVolatility", () => model.LognormalVolatility(strikes));
                    check.Output(c, "normalVolatility", () => model.NormalVolatility(strikes));
                    check.Output(c, "localVolatility", () => model.LocalVolatility(strikes));
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }

    private static ZabrModel ModelOf(JsonElement spec) => new(
        Number(spec.GetProperty("expiryTime")),
        Number(spec.GetProperty("forward")),
        Number(spec.GetProperty("alpha")),
        Number(spec.GetProperty("beta")),
        Number(spec.GetProperty("nu")),
        Number(spec.GetProperty("rho")),
        Number(spec.GetProperty("gamma")));

    private static void CheckModel(VectorAssert check, VectorCase c, ZabrModel model)
    {
        double[] strikes = Numbers(c.Input("strikes"));
        double[] points = Numbers(c.Input("points"));
        check.Output(c, "forward", model.Forward);
        check.Output(c, "expiryTime", model.ExpiryTime);
        check.Output(c, "alpha", model.Alpha);
        check.Output(c, "beta", model.Beta);
        check.Output(c, "nu", model.Nu);
        check.Output(c, "rho", model.Rho);
        check.Output(c, "gamma", model.Gamma);
        check.Output(c, "lognormalVolatility", () => model.LognormalVolatility(strikes));
        check.Output(c, "normalVolatility", () => model.NormalVolatility(strikes));
        check.Output(c, "localVolatility", () => model.LocalVolatility(strikes));
        check.Output(c, "lognormalVolatilityAt", AtPoints(points, model.LognormalVolatility));
        check.Output(c, "normalVolatilityAt", AtPoints(points, model.NormalVolatility));
        check.Output(c, "localVolatilityAt", AtPoints(points, model.LocalVolatility));
        check.Output(c, "fdPrice", () => model.FdPrice(Numbers(c.Input("fdStrikes"))));
        check.Output(c, "fdPriceAt", AtPoints(points, model.FdPrice));
    }

    // Each value computed when compared, so that a throw is checked.
    private static Func<object?>[] AtPoints(double[] points, Func<double, double> f) =>
        [.. points.Select(k => (Func<object?>)(() => f(k)))];
}
