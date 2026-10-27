// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's accounting-engine vectors, <c>tests/vectors/cxx/models/marketmodels/accountingengine.json</c>:
/// <see cref="AccountingEngine"/>'s values over a few paths of the Euler, predictor-corrector and normal evolvers, read
/// through the statistics' samples, weight sum, means, variances and error estimates.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AccountingEngineCxxVectorTests
{
    [Fact]
    public void AccountingEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/accountingengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            MarketModel model = ModelOf(c.Input("model"));
            BrownianGeneratorFactory factory = FactoryOf(c.Input("generator"));
            int[] numeraires = Integers(c.Input("numeraires"));
            MarketModelEvolver evolver = c.String("evolver") switch
            {
                "LogNormalFwdRateEuler" => new LogNormalFwdRateEuler(model, factory, numeraires),
                "LogNormalFwdRatePc" => new LogNormalFwdRatePc(model, factory, numeraires),
                _ => new NormalFwdRatePc(model, factory, numeraires),
            };
            MarketModelMultiProduct product = MarketModelProductCxxInputs.ProductOf(c.Input("product"));
            var engine = new AccountingEngine(evolver, product, c.Double("initialNumeraireValue"));
            var stats = new SequenceStatisticsInc(product.NumberOfProducts);
            engine.MultiplePathValues(stats, c.Input("paths").GetInt32());
            check.Output(c, "samples", stats.Samples);
            check.Output(c, "weightSum", stats.WeightSum());
            check.Output(c, "mean", stats.Mean());
            check.Output(c, "variance", Lazy(() => stats.Variance()));
            check.Output(c, "errorEstimate", Lazy(() => stats.ErrorEstimate()));
        }

        check.Complete();
    }
}
