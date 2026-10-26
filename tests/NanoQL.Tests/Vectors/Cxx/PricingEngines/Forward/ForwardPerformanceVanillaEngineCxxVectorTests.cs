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
/// The ForwardPerformanceVanillaEngine vectors,
/// <c>tests/vectors/cxx/pricingengines/forward/forwardperformanceengine.json</c>:
/// <see cref="ForwardPerformanceVanillaEngine"/> over <see cref="AnalyticEuropeanEngine"/>, its NPV and Greeks for
/// forward-start performance calls and puts on flat markets, against QuantLib's through the C++ harness.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ForwardPerformanceVanillaEngineCxxVectorTests
{
    [Fact]
    public void ForwardPerformanceVanillaEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/forward/forwardperformanceengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Date today = context.EvaluationDate;
            DayCounter dc = new Actual365Fixed();
            JsonElement p = c.Input("process");
            var process = new BlackScholesMertonProcess(
                p[0].GetDouble(),
                new FlatForward(today, p[2].GetDouble(), dc),
                new FlatForward(today, p[1].GetDouble(), dc),
                new BlackConstantVol(today, new NullCalendar(), p[3].GetDouble(), dc));
            JsonElement o = c.Input("option");
            var option = new ForwardVanillaOption(
                o[0].GetDouble(),
                new Date(o[1].GetInt32()),
                new PlainVanillaPayoff((OptionType)o[2].GetInt32(), 0.0),
                new EuropeanExercise(new Date(o[3].GetInt32())));
            var r = new Lazy<OneAssetOptionResults>(
                () => ForwardPerformanceVanillaEngine.Calculate(option, context, process, AnalyticEuropeanEngine.Calculate));
            AnalyticCliquetEngineVectorTests.CheckGreeks(check, c, r);
        }

        check.Complete();
    }
}
