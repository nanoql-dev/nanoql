// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's binomial double-barrier vectors on the default asset,
/// <c>tests/vectors/cxx/experimental/barrieroption/discretizeddoublebarrieroption.json</c>:
/// <see cref="BinomialDoubleBarrierEngine"/> on <see cref="DiscretizedDoubleBarrierOption"/> and Cox-Ross-Rubinstein
/// trees, the four barrier types, European, American and Bermudan, their NPV, delta, gamma and theta, and each asset's
/// mandatory times on no grid and on two; the process and option as
/// <c>tools/cxx/src/experimental/barrieroption/discretizeddoublebarrieroption.cpp</c> builds them. The outputs nanoql
/// fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DiscretizedDoubleBarrierOptionCxxVectorTests
{
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly string[] Outputs = ["NPV", "delta", "gamma", "theta"];

    [Fact]
    public void DiscretizedDoubleBarrierOptionCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/barrieroption/discretizeddoublebarrieroption");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement s = c.Input("option");
            if (c.Inputs.TryGetProperty("enhanced", out _))
            {
                (GeneralizedBlackScholesProcess p, DoubleBarrierOption o, Date last) = Setup(s);
                int steps = s.GetProperty("steps").GetInt32();
                TimeGrid? grid = steps > 0 ? new TimeGrid(p.Time(last), steps) : null;
                DiscretizedAsset asset = c.Bool("enhanced")
                    ? new DiscretizedDermanKaniDoubleBarrierOption(o, p, grid)
                    : new DiscretizedDoubleBarrierOption(o, p, grid);
                check.Output(c, "mandatoryTimes", asset.MandatoryTimes().Select(t => (object?)t).ToArray());
                continue;
            }

            var priced = new Lazy<OneAssetOptionResults>(() => Price(s, context));
            foreach (string output in Outputs.Where(o => c.Expected.TryGetProperty(o, out _)))
            {
                check.Output(c, output, Lazy(() => output switch
                {
                    "NPV" => priced.Value.NPV,
                    "delta" => priced.Value.Delta,
                    "gamma" => priced.Value.Gamma,
                    _ => priced.Value.Theta,
                }));
            }
        }

        check.Complete();
    }

    private static OneAssetOptionResults Price(JsonElement s, EvaluationContext context)
    {
        (GeneralizedBlackScholesProcess process, DoubleBarrierOption option, _) = Setup(s);
        return BinomialDoubleBarrierEngine.Calculate<CoxRossRubinstein, DiscretizedDoubleBarrierOption>(
            option, context, process, s.GetProperty("steps").GetInt32());
    }

    // The harness's process and option, and the exercise's last date
    private static (GeneralizedBlackScholesProcess Process, DoubleBarrierOption Option, Date Last) Setup(JsonElement s)
    {
        var today = new Date(s.GetProperty("referenceDate").GetInt32());
        var process = new BlackScholesMertonProcess(
            s.GetProperty("spot").GetDouble(),
            new FlatForward(today, s.GetProperty("dividendYield").GetDouble(), A365),
            new FlatForward(today, s.GetProperty("riskFreeRate").GetDouble(), A365),
            new BlackConstantVol(today, new NullCalendar(), s.GetProperty("vol").GetDouble(), A365));
        Date[] dates = [.. s.GetProperty("days").EnumerateArray().Select(n => today + n.GetInt32())];
        Exercise exercise = s.GetProperty("exerciseType").GetString() switch
        {
            "European" => new EuropeanExercise(dates[^1]),
            "American" => new AmericanExercise(dates[0], dates[^1]),
            _ => new BermudanExercise(dates),
        };
        var option = new DoubleBarrierOption(
            (DoubleBarrierType)s.GetProperty("barrierType").GetInt32(),
            s.GetProperty("barrierLo").GetDouble(),
            s.GetProperty("barrierHi").GetDouble(),
            s.GetProperty("rebate").GetDouble(),
            new PlainVanillaPayoff((OptionType)s.GetProperty("optionType").GetInt32(), s.GetProperty("strike").GetDouble()),
            exercise);
        return (process, option, dates[^1]);
    }
}
