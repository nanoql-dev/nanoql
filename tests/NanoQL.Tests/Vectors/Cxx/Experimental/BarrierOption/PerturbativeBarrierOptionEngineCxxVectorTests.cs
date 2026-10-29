// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's perturbative barrier vectors,
/// <c>tests/vectors/cxx/experimental/barrieroption/perturbativebarrieroptionengine.json</c>:
/// <see cref="PerturbativeBarrierOptionEngine"/>'s up-and-out puts at order 0 over three volatility markets, with the
/// engine's γ and with γ 0, and its refusals; the process and option as
/// <c>tools/cxx/src/experimental/barrieroption/perturbativebarrieroptionengine.cpp</c> builds them. The outputs nanoql
/// fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PerturbativeBarrierOptionEngineCxxVectorTests
{
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void PerturbativeBarrierOptionEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/barrieroption/perturbativebarrieroptionengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement s = c.Input("option");
            check.Output(c, "NPV", Lazy(() => Price(s, context)));
        }

        check.Complete();
    }

    private static double? Price(JsonElement s, EvaluationContext context)
    {
        var today = new Date(s.GetProperty("referenceDate").GetInt32());
        Date[] dates = [.. s.GetProperty("varianceDays").EnumerateArray().Select(n => today + n.GetInt32())];
        var process = new BlackScholesMertonProcess(
            s.GetProperty("spot").GetDouble(),
            new FlatForward(today, s.GetProperty("dividendYield").GetDouble(), A365),
            new FlatForward(today, s.GetProperty("riskFreeRate").GetDouble(), A365),
            new BlackVarianceCurve(today, dates, Doubles(s.GetProperty("vols")), A365));
        var option = new BarrierOption(
            (BarrierType)s.GetProperty("barrierType").GetInt32(),
            s.GetProperty("barrier").GetDouble(),
            s.GetProperty("rebate").GetDouble(),
            new PlainVanillaPayoff((OptionType)s.GetProperty("optionType").GetInt32(), s.GetProperty("strike").GetDouble()),
            new EuropeanExercise(today + s.GetProperty("expiryDays").GetInt32()));
        return PerturbativeBarrierOptionEngine.Calculate(
            option,
            context,
            process,
            s.GetProperty("order").GetInt32(),
            s.GetProperty("zeroGamma").GetBoolean(),
            s.GetProperty("npoint").GetInt32(),
            s.GetProperty("npoint2").GetInt32()).NPV;
    }
}
