// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="AnalyticEuropeanEngine"/>'s harness vectors,
/// <c>tests/vectors/cxx/pricingengines/vanilla/analyticeuropeanengine.json</c>: the additional results of European
/// plain vanilla options on eight processes over flat forwards and a constant volatility, with and without a discount
/// curve.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticEuropeanEngineCxxVectorTests
{
    private static readonly string[] Keys =
        ["spot", "dividendDiscount", "riskFreeDiscount", "forward", "strike", "volatility", "timeToExpiry"];

    [Fact]
    public void AnalyticEuropeanEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/vanilla/analyticeuropeanengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Date reference = c.DateInput("referenceDate");
            DayCounter dc = CashFlowInputs.DayCounterOf(c.Input("dayCounter"));
            var process = new BlackScholesMertonProcess(
                c.Double("spot"),
                new FlatForward(reference, c.Double("dividendYield"), dc),
                new FlatForward(reference, c.Double("riskFreeRate"), dc),
                new BlackConstantVol(reference, new NullCalendar(), c.Double("volatility"), dc));
            var option = new VanillaOption(
                new PlainVanillaPayoff((OptionType)c.Int("optionType"), c.Double("strike")),
                new EuropeanExercise(c.DateInput("exerciseDate")));
            OneAssetOptionResults results = c.Input("discountRate").ValueKind == JsonValueKind.Null
                ? AnalyticEuropeanEngine.Calculate(option, context, process)
                : AnalyticEuropeanEngine.Calculate(option, context, process, new FlatForward(reference, c.Double("discountRate"), dc));
            foreach (string key in Keys)
            {
                check.Output(c, key, results.Result<double>(key));
            }
        }

        check.Complete();
    }
}
