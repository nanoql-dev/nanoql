// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="AnalyticBlackVasicekEngine"/>'s harness vectors,
/// <c>tests/vectors/cxx/pricingengines/vanilla/analyticeuropeanvasicekengine.json</c>: the NPVs of European calls and
/// puts over flat forwards, no dividends and a constant volatility, with three Vasicek models and three correlations,
/// and the refusal of an American exercise.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticBlackVasicekEngineCxxVectorTests
{
    [Fact]
    public void AnalyticBlackVasicekEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/vanilla/analyticeuropeanvasicekengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Date reference = c.DateInput("referenceDate");
            DayCounter dc = CashFlowInputs.DayCounterOf(c.Input("dayCounter"));
            var process = new BlackScholesMertonProcess(
                c.Double("spot"),
                new FlatForward(reference, 0.0, dc),
                new FlatForward(reference, c.Double("riskFreeRate"), dc),
                new BlackConstantVol(reference, new NullCalendar(), c.Double("volatility"), dc));
            Exercise exercise = c.String("exerciseType") == "European"
                ? new EuropeanExercise(c.DateInput("exerciseDate"))
                : new AmericanExercise(reference, c.DateInput("exerciseDate"));
            var option = new VanillaOption(
                new PlainVanillaPayoff((OptionType)c.Int("optionType"), c.Double("strike")), exercise);
            JsonElement m = c.Input("model");
            var model = new Vasicek(
                m.GetProperty("r0").GetDouble(),
                m.GetProperty("a").GetDouble(),
                m.GetProperty("b").GetDouble(),
                m.GetProperty("sigma").GetDouble(),
                m.GetProperty("lambda").GetDouble());
            check.Output(c, "NPV", Lazy(() => AnalyticBlackVasicekEngine.Calculate(
                option, context, process, model, c.Double("correlation")).NPV));
        }

        check.Complete();
    }
}
