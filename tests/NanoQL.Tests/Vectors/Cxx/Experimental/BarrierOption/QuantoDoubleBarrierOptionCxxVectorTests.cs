// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's quanto double-barrier vectors,
/// <c>tests/vectors/cxx/experimental/barrieroption/quantodoublebarrieroption.json</c>: <see cref="QuantoEngine"/> on
/// <see cref="QuantoDoubleBarrierOption"/> with <see cref="AnalyticDoubleBarrierEngine"/>, knock-ins and knock-outs on
/// two corridors at three correlations, and an expired option; the process and option as
/// <c>tools/cxx/src/experimental/barrieroption/quantodoublebarrieroption.cpp</c> builds them. The outputs nanoql fixes
/// are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class QuantoDoubleBarrierOptionCxxVectorTests
{
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void QuantoDoubleBarrierOptionCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/barrieroption/quantodoublebarrieroption");
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
        var process = new BlackScholesMertonProcess(
            s.GetProperty("spot").GetDouble(),
            new FlatForward(today, s.GetProperty("dividendYield").GetDouble(), A365),
            new FlatForward(today, s.GetProperty("riskFreeRate").GetDouble(), A365),
            new BlackConstantVol(today, new NullCalendar(), s.GetProperty("vol").GetDouble(), A365));
        var option = new QuantoDoubleBarrierOption(
            (DoubleBarrierType)s.GetProperty("barrierType").GetInt32(),
            s.GetProperty("barrierLo").GetDouble(),
            s.GetProperty("barrierHi").GetDouble(),
            s.GetProperty("rebate").GetDouble(),
            new PlainVanillaPayoff((OptionType)s.GetProperty("optionType").GetInt32(), s.GetProperty("strike").GetDouble()),
            new EuropeanExercise(today + s.GetProperty("expiryDays").GetInt32()));
        return QuantoEngine.Calculate(
            option,
            context,
            process,
            new FlatForward(today, s.GetProperty("foreignRate").GetDouble(), A365),
            new BlackConstantVol(today, new NullCalendar(), s.GetProperty("fxVol").GetDouble(), A365),
            s.GetProperty("correlation").GetDouble(),
            (o, ctx, p) => AnalyticDoubleBarrierEngine.Calculate(o, ctx, p)).NPV;
    }
}
