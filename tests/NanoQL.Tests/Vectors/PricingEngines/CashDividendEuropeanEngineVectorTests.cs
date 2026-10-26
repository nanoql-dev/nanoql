// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The CashDividendEuropeanEngine vectors, <c>tests/vectors/pricingengines/vanilla/cashdividendeuropeanengine.json</c>:
/// European plain-vanilla calls and puts over three processes and seven dividend schedules, by the spot and the
/// escrowed models, their NPVs, and the refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CashDividendEuropeanEngineVectorTests
{
    [Fact]
    public void CashDividendEuropeanEngineVector()
    {
        var file = VectorFile.Load("pricingengines/vanilla/cashdividendeuropeanengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            Exercise exercise = ExerciseOf(c.Input("exercise"), context);
            ImmutableArray<Dividend> dividends = DividendsOf(c.Input("dividends"));
            var model = (CashDividendEuropeanEngineCashDividendModel)c.Int("model");
            check.Output(c, "NPV", c.Input("payoffs").EnumerateArray()
                .Select(p => Lazy(() => CashDividendEuropeanEngine.Calculate(
                    new VanillaOption(PayoffOf(p), exercise), context, process, dividends, model).NPV))
                .ToArray());
        }

        check.Complete();
    }
}
