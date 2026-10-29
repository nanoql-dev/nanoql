// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Monte Carlo double-barrier vectors,
/// <c>tests/vectors/cxx/experimental/barrieroption/mcdoublebarrierengine.json</c>: <see cref="MCDoubleBarrierEngine"/>'s
/// fixed-seed NPV and error estimate through <see cref="MakeMCDoubleBarrierEngine{TRng, TStats}"/> on pseudo-random
/// paths, its refusals of a KIKO and of a spot on a barrier, and <see cref="DoubleBarrierPathPricer"/> on given paths;
/// the process and option as <c>tools/cxx/src/experimental/barrieroption/mcdoublebarrierengine.cpp</c> builds them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MCDoubleBarrierEngineCxxVectorTests
{
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void MCDoubleBarrierEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/barrieroption/mcdoublebarrierengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Inputs.TryGetProperty("pathPricer", out JsonElement p))
            {
                var pricer = new DoubleBarrierPathPricer(
                    (DoubleBarrierType)p.GetProperty("barrierType").GetInt32(),
                    p.GetProperty("barrierLo").GetDouble(),
                    p.GetProperty("barrierHi").GetDouble(),
                    p.GetProperty("rebate").GetDouble(),
                    (OptionType)p.GetProperty("optionType").GetInt32(),
                    p.GetProperty("strike").GetDouble(),
                    Doubles(p.GetProperty("discounts")));
                var path = new MonteCarloPath(
                    new TimeGrid(Doubles(p.GetProperty("times"))), new Vector(Doubles(p.GetProperty("values"))));
                check.Output(c, "value", pricer.Evaluate(path));
                continue;
            }

            JsonElement s = c.Input("option");
            var priced = new Lazy<OneAssetOptionResults>(() => Price(s, context));
            check.Output(c, "NPV", Lazy(() => priced.Value.NPV));
            if (c.Expected.TryGetProperty("errorEstimate", out _))
            {
                check.Output(c, "errorEstimate", Lazy(() => priced.Value.ErrorEstimate));
            }
        }

        check.Complete();
    }

    // The harness's builder: pseudo-random, the steps, the antithetic flag, the samples and the seed
    private static OneAssetOptionResults Price(JsonElement s, EvaluationContext context)
    {
        var today = new Date(s.GetProperty("referenceDate").GetInt32());
        var process = new BlackScholesMertonProcess(
            s.GetProperty("spot").GetDouble(),
            new FlatForward(today, s.GetProperty("dividendYield").GetDouble(), A365),
            new FlatForward(today, s.GetProperty("riskFreeRate").GetDouble(), A365),
            new BlackConstantVol(today, new NullCalendar(), s.GetProperty("vol").GetDouble(), A365));
        var option = new DoubleBarrierOption(
            (DoubleBarrierType)s.GetProperty("barrierType").GetInt32(),
            s.GetProperty("barrierLo").GetDouble(),
            s.GetProperty("barrierHi").GetDouble(),
            s.GetProperty("rebate").GetDouble(),
            new PlainVanillaPayoff((OptionType)s.GetProperty("optionType").GetInt32(), s.GetProperty("strike").GetDouble()),
            new EuropeanExercise(today + s.GetProperty("expiryDays").GetInt32()));
        return new MakeMCDoubleBarrierEngine<PseudoRandom, GeneralStatistics>(process, s.GetProperty("seed").GetInt64())
            .WithSteps(s.GetProperty("steps").GetInt32())
            .WithAntitheticVariate(s.GetProperty("antithetic").GetBoolean())
            .WithSamples(s.GetProperty("samples").GetInt32())
            .ToEngine()(option, context);
    }
}
