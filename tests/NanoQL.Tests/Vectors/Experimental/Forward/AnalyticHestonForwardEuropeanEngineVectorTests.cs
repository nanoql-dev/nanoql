// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AnalyticHestonForwardEuropeanEngine vectors,
/// <c>tests/vectors/experimental/forward/analytichestonforwardeuropeanengine.json</c>: forward-starting calls and puts on
/// three Heston processes, reset today and up to six months ahead, expiring in one and two years; an expired option.
/// The outputs nanoql fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticHestonForwardEuropeanEngineVectorTests
{
    [Fact]
    public void AnalyticHestonForwardEuropeanEngineVector()
    {
        var file = VectorFile.Load("experimental/forward/analytichestonforwardeuropeanengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            var engine = new AnalyticHestonForwardEuropeanEngine(HestonProcessOf(c.Input("process")));
            var reset = new Date(c.Int("resetDate"));
            var exercise = new EuropeanExercise(new Date(c.Int("exerciseDate")));
            check.Output(
                c,
                "NPV",
                c.Input("payoffs").EnumerateArray()
                    .Select(p => Lazy(() => engine.Calculate(
                        new ForwardVanillaOption(
                            p.GetProperty("moneyness").GetDouble(),
                            reset,
                            new PlainVanillaPayoff((OptionType)p.GetProperty("optionType").GetInt32(), 0.0),
                            exercise),
                        context).NPV))
                    .ToArray());
        }

        check.Complete();
    }

    // tools/vectors/gen/processes/hestonprocess.py's spec
    internal static HestonProcess HestonProcessOf(JsonElement s) =>
        new(
            CurveOf(s.GetProperty("riskFree")),
            CurveOf(s.GetProperty("dividend")),
            s.GetProperty("s0").GetDouble(),
            s.GetProperty("v0").GetDouble(),
            s.GetProperty("kappa").GetDouble(),
            s.GetProperty("theta").GetDouble(),
            s.GetProperty("sigma").GetDouble(),
            s.GetProperty("rho").GetDouble());
}
