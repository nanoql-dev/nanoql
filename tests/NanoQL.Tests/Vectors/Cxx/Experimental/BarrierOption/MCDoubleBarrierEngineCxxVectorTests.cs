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
/// paths, and through <see cref="MCPRDoubleBarrierEngine"/>; its NPV through <see cref="MCLDDoubleBarrierEngine"/> on
/// low-discrepancy paths; its refusals of a KIKO and of a spot on a barrier, and <see cref="DoubleBarrierPathPricer"/>
/// on given paths; the process and option as <c>tools/cxx/src/experimental/barrieroption/mcdoublebarrierengine.cpp</c>
/// builds them.
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

    [Fact]
    public void MCPRDoubleBarrierEngineCxxVector()
    {
        // MCPRDoubleBarrierEngine on the pseudo-random cases, which the fact above prices through the builder:
        // QuantLib's same closing, so its same outputs
        var file = VectorFile.Load("cxx/experimental/barrieroption/mcdoublebarrierengine");
        var check = new VectorAssert(file);
        VectorCase[] cases =
        [
            .. file.Cases.Where(c => c.Inputs.TryGetProperty("option", out JsonElement s) && s.GetProperty("traits").GetString() == "pr"),
        ];
        foreach (VectorCase c in cases)
        {
            EvaluationContext context = c.Context();
            JsonElement s = c.Input("option");
            var priced = new Lazy<OneAssetOptionResults>(() =>
            {
                (GeneralizedBlackScholesProcess process, DoubleBarrierOption option) = MarketOf(s);
                return MCPRDoubleBarrierEngine.Calculate(
                    option, context, process, s.GetProperty("steps").GetInt32(), null, false, s.GetProperty("antithetic").GetBoolean(),
                    s.GetProperty("samples").GetInt32(), null, null, s.GetProperty("seed").GetInt64());
            });
            check.Output(c, "NPV", Lazy(() => priced.Value.NPV));
            if (c.Expected.TryGetProperty("errorEstimate", out _))
            {
                check.Output(c, "errorEstimate", Lazy(() => priced.Value.ErrorEstimate));
            }
        }

        Assert.Empty(check.Mismatches);
        Assert.Equal(cases.Sum(c => c.Expected.EnumerateObject().Count()), check.Compared);
    }

    // The harness's engine: the builder on pseudo-random paths ("pr"), MCLDDoubleBarrierEngine on low-discrepancy ones
    // ("ld"), each with the steps, the antithetic flag, the samples and the seed
    private static OneAssetOptionResults Price(JsonElement s, EvaluationContext context)
    {
        (GeneralizedBlackScholesProcess process, DoubleBarrierOption option) = MarketOf(s);
        int steps = s.GetProperty("steps").GetInt32();
        bool antithetic = s.GetProperty("antithetic").GetBoolean();
        int samples = s.GetProperty("samples").GetInt32();
        long seed = s.GetProperty("seed").GetInt64();
        return s.GetProperty("traits").GetString() == "pr"
            ? new MakeMCDoubleBarrierEngine<PseudoRandom, GeneralStatistics>(process, seed)
                .WithSteps(steps)
                .WithAntitheticVariate(antithetic)
                .WithSamples(samples)
                .ToEngine()(option, context)
            : MCLDDoubleBarrierEngine.Calculate(option, context, process, steps, null, false, antithetic, samples, null, null, seed);
    }

    // The harness's process and option
    private static (GeneralizedBlackScholesProcess Process, DoubleBarrierOption Option) MarketOf(JsonElement s)
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
        return (process, option);
    }
}
