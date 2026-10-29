// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's variance option vectors,
/// <c>tests/vectors/cxx/experimental/varianceoption/integralhestonvarianceoptionengine.json</c>:
/// <see cref="IntegralHestonVarianceOptionEngine"/>'s NPV for calls on the realized variance on three processes, and a
/// refusal; the process and option as
/// <c>tools/cxx/src/experimental/varianceoption/integralhestonvarianceoptionengine.cpp</c> builds them, its empty
/// dividend handle a flat zero curve, which nanoql's engine doesn't read.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class IntegralHestonVarianceOptionEngineCxxVectorTests
{
    private static readonly DayCounter A360 = new Actual360();

    [Fact]
    public void IntegralHestonVarianceOptionEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/varianceoption/integralhestonvarianceoptionengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement s = c.Input("option");
            Date today = context.EvaluationDate;
            var process = new HestonProcess(
                new FlatForward(today, s.GetProperty("riskFreeRate").GetDouble(), A360),
                new FlatForward(today, 0.0, A360),
                1.0,
                s.GetProperty("v0").GetDouble(),
                s.GetProperty("kappa").GetDouble(),
                s.GetProperty("theta").GetDouble(),
                s.GetProperty("sigma").GetDouble(),
                s.GetProperty("rho").GetDouble());
            var option = new VarianceOption(
                new PlainVanillaPayoff((OptionType)s.GetProperty("optionType").GetInt32(), s.GetProperty("strike").GetDouble()),
                s.GetProperty("notional").GetDouble(),
                today,
                today + s.GetProperty("maturityDays").GetInt32());
            check.Output(c, "NPV", () => IntegralHestonVarianceOptionEngine.Calculate(option, context, process).NPV);
        }

        check.Complete();
    }
}
