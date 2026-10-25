// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's COS engine vectors, <c>tests/vectors/cxx/pricingengines/vanilla/coshestonengine.json</c>: the
/// normalized characteristic function at six u and three times, and the cumulants, mean, variance, skew and kurtosis at
/// those times, on three Heston models at two truncations and orders, every κt at or above the cumulants' cuts.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class COSHestonEngineCxxVectorTests
{
    [Fact]
    public void COSHestonEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/vanilla/coshestonengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var engine = new COSHestonEngine(
                new HestonModel(HestonCxxInputs.ProcessOf(c.Input("process"))), c.Double("L"), c.Input("N").GetInt32());
            double[] us = c.Doubles("us"), ts = c.Doubles("ts");
            check.Output(c, "chF", us.SelectMany(u => ts.Select(t => Lazy(() => Parts(engine.ChF(u, t))))).ToArray());
            check.Output(c, "c1", ts.Select(t => Lazy(() => engine.C1(t))).ToArray());
            check.Output(c, "c2", ts.Select(t => Lazy(() => engine.C2(t))).ToArray());
            check.Output(c, "c3", ts.Select(t => Lazy(() => engine.C3(t))).ToArray());
            check.Output(c, "c4", ts.Select(t => Lazy(() => engine.C4(t))).ToArray());
            check.Output(c, "mu", ts.Select(t => Lazy(() => engine.Mu(t))).ToArray());
            check.Output(c, "var", ts.Select(t => Lazy(() => engine.Var(t))).ToArray());
            check.Output(c, "skew", ts.Select(t => Lazy(() => engine.Skew(t))).ToArray());
            check.Output(c, "kurtosis", ts.Select(t => Lazy(() => engine.Kurtosis(t))).ToArray());
        }

        check.Complete();
    }

    // a complex value as the vectors write it, [real, imaginary]
    internal static double[] Parts(System.Numerics.Complex z) => [z.Real, z.Imaginary];
}
