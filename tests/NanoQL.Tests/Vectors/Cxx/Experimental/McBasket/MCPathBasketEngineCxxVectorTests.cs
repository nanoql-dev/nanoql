// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's path engine vectors, <c>tests/vectors/cxx/experimental/mcbasket/mcpathbasketengine.json</c>:
/// <see cref="MCPathBasketEngine"/>'s fixed-seed NPV, and its error estimate under the pseudo-random traits, for an
/// average call and an Asian basket call, and <see cref="MCAmericanPathEngine{TRng, TStats}"/>'s for a Bermudan put on
/// the largest asset, each through its builder and also on payoffs deriving from <see cref="AdaptedPathPayoff"/>, the
/// path basket's pseudo-random cases also through <see cref="MCPRPathBasketEngine"/> and its low-discrepancy ones
/// through <see cref="MCLDPathBasketEngine"/>; the payoffs, processes and options as
/// <c>tools/cxx/src/experimental/mcbasket/mcpathbasketengine.cpp</c> builds them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MCPathBasketEngineCxxVectorTests
{
    [Fact]
    public void MCPathBasketEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/mcbasket/mcpathbasketengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement e = c.Input("engine");
            Date today = context.EvaluationDate;
            StochasticProcessArray processes = MCPagodaEngineCxxVectorTests.ProcessesOf(
                e.GetProperty("processes"), e.GetProperty("correlation").GetDouble(), today);
            JsonElement payoff = e.GetProperty("payoff");
            string kind = payoff[0].GetString()!;
            PathPayoff pathPayoff = kind.StartsWith("Adapted", StringComparison.Ordinal)
                ? new HarnessAdaptedPayoff(kind, payoff[1].GetDouble(), processes.Size)
                : new HarnessPayoff(kind, payoff[1].GetDouble(), processes.Size);
            var option = new PathOption(
                pathPayoff, [.. e.GetProperty("fixingDays").EnumerateArray().Select(d => today + d.GetInt32())]);
            int? steps = NullableInt(e.GetProperty("timeSteps"));
            int? perYear = NullableInt(e.GetProperty("timeStepsPerYear"));
            bool antithetic = e.GetProperty("antithetic").GetBoolean();
            int samples = e.GetProperty("samples").GetInt32();
            long seed = e.GetProperty("seed").GetInt64();
            if (e.GetProperty("engine").GetString() == "american")
            {
                // the harness's builder: the steps or the steps per year, the antithetic flag, the samples, the seed and
                // the calibration samples where given
                MakeMCAmericanPathEngine<PseudoRandom, GeneralStatistics> builder =
                    new MakeMCAmericanPathEngine<PseudoRandom, GeneralStatistics>(processes, seed)
                        .WithAntitheticVariate(antithetic)
                        .WithSamples(samples);
                builder = steps is int s ? builder.WithSteps(s) : builder.WithStepsPerYear(perYear!.Value);
                if (NullableInt(e.GetProperty("calibrationSamples")) is int calibration)
                {
                    builder = builder.WithCalibrationSamples(calibration);
                }

                var priced = new Lazy<InstrumentResults>(() => builder.ToEngine()(option, context));
                check.Output(c, "NPV", () => priced.Value.NPV);
                check.Output(c, "errorEstimate", () => priced.Value.ErrorEstimate);
            }
            else if (e.GetProperty("traits").GetString() == "pr")
            {
                MakeMCPathBasketEngine<PseudoRandom, GeneralStatistics> builder =
                    new MakeMCPathBasketEngine<PseudoRandom, GeneralStatistics>(processes, seed)
                        .WithAntitheticVariate(antithetic)
                        .WithSamples(samples);
                builder = steps is int s ? builder.WithSteps(s) : builder.WithStepsPerYear(perYear!.Value);
                var priced = new Lazy<InstrumentResults>(() => builder.ToEngine()(option, context));
                check.Output(c, "NPV", () => priced.Value.NPV);
                check.Output(c, "errorEstimate", () => priced.Value.ErrorEstimate);
            }
            else
            {
                check.Output(c, "NPV", () => MCLDPathBasketEngine.Calculate(
                    option, context, processes, steps, perYear, false, antithetic, false, samples, null, null, seed).NPV);
            }
        }

        check.Complete();
    }

    [Fact]
    public void MCPRPathBasketEngineCxxVector()
    {
        // MCPRPathBasketEngine on the path basket's pseudo-random cases, which the fact above prices through the
        // builder: QuantLib's same closing, so its same outputs
        var file = VectorFile.Load("cxx/experimental/mcbasket/mcpathbasketengine");
        var check = new VectorAssert(file);
        VectorCase[] cases =
        [
            .. file.Cases.Where(c => c.Input("engine").GetProperty("engine").GetString() != "american"
                && c.Input("engine").GetProperty("traits").GetString() == "pr"),
        ];
        foreach (VectorCase c in cases)
        {
            EvaluationContext context = c.Context();
            JsonElement e = c.Input("engine");
            Date today = context.EvaluationDate;
            StochasticProcessArray processes = MCPagodaEngineCxxVectorTests.ProcessesOf(
                e.GetProperty("processes"), e.GetProperty("correlation").GetDouble(), today);
            JsonElement payoff = e.GetProperty("payoff");
            string kind = payoff[0].GetString()!;
            PathPayoff pathPayoff = kind.StartsWith("Adapted", StringComparison.Ordinal)
                ? new HarnessAdaptedPayoff(kind, payoff[1].GetDouble(), processes.Size)
                : new HarnessPayoff(kind, payoff[1].GetDouble(), processes.Size);
            var option = new PathOption(
                pathPayoff, [.. e.GetProperty("fixingDays").EnumerateArray().Select(d => today + d.GetInt32())]);
            var priced = new Lazy<InstrumentResults>(() => MCPRPathBasketEngine.Calculate(
                option,
                context,
                processes,
                NullableInt(e.GetProperty("timeSteps")),
                NullableInt(e.GetProperty("timeStepsPerYear")),
                false,
                e.GetProperty("antithetic").GetBoolean(),
                false,
                e.GetProperty("samples").GetInt32(),
                null,
                null,
                e.GetProperty("seed").GetInt64()));
            check.Output(c, "NPV", () => priced.Value.NPV);
            check.Output(c, "errorEstimate", () => priced.Value.ErrorEstimate);
        }

        Assert.Empty(check.Mismatches);
        Assert.Equal(cases.Sum(c => c.Expected.EnumerateObject().Count()), check.Compared);
    }

    private static int? NullableInt(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : e.GetInt32();

    // the harness's path option: its payoff and fixing dates
    private sealed class PathOption(PathPayoff payoff, ImmutableArray<Date> fixingDates) : PathMultiAssetOption
    {
        public override PathPayoff PathPayoff => payoff;

        public override ImmutableArray<Date> FixingDates => fixingDates;
    }

    // the harness's adapted payoffs, through the valuation data: "AdaptedAverageCall" and "AdaptedBermudanMaxPut"
    private sealed class HarnessAdaptedPayoff(string kind, double strike, int assets) : AdaptedPathPayoff
    {
        public override string Name => kind;

        public override string Description => kind;

        public override int BasisSystemDimension => assets;

        protected override void Evaluate(AdaptedPathPayoffValuationData data)
        {
            int n = data.NumberOfAssets, m = data.NumberOfTimes;
            if (kind == "AdaptedAverageCall")
            {
                double sum = 0.0;
                for (int j = 0; j < n; ++j)
                {
                    sum += data.GetAssetValue(m - 1, j);
                }

                double deferral = data.GetYieldTermStructure(m - 1).Discount(0.25);
                data.SetPayoffValue(m - 1, Math.Max((sum / n) - strike, 0.0) * deferral);
            }
            else
            {
                for (int i = 0; i < m; ++i)
                {
                    var state = new Vector(n);
                    state[0] = data.GetAssetValue(i, 0);
                    double best = state[0];
                    for (int j = 1; j < n; ++j)
                    {
                        state[j] = data.GetAssetValue(i, j);
                        best = Math.Max(best, state[j]);
                    }

                    data.SetExerciseData(i, Math.Max(strike - best, 0.0), ref state);
                }
            }
        }
    }

    // the harness's payoffs, in its arithmetic's order: "AverageCall", "AsianBasketCall" and "BermudanMaxPut"
    private sealed class HarnessPayoff(string kind, double strike, int assets) : PathPayoff
    {
        public override string Name => kind;

        public override string Description => kind;

        public override int BasisSystemDimension => assets;

        public override void Value(
            Matrix path, ImmutableArray<YieldTermStructure> forwardTermStructures, Vector payments, Vector exercises, Vector[] states)
        {
            int n = path.Rows, m = path.Columns;
            if (kind == "AverageCall")
            {
                double sum = 0.0;
                for (int j = 0; j < n; ++j)
                {
                    sum += path[j, m - 1];
                }

                payments[m - 1] = Math.Max((sum / n) - strike, 0.0);
            }
            else if (kind == "AsianBasketCall")
            {
                double sum = 0.0;
                for (int i = 0; i < m; ++i)
                {
                    for (int j = 0; j < n; ++j)
                    {
                        sum += path[j, i];
                    }
                }

                payments[m - 1] = Math.Max((sum / (n * m)) - strike, 0.0);
            }
            else
            {
                for (int i = 0; i < m; ++i)
                {
                    double best = path[0, i];
                    for (int j = 1; j < n; ++j)
                    {
                        best = Math.Max(best, path[j, i]);
                    }

                    payments[i] = 0.0;
                    exercises[i] = Math.Max(strike - best, 0.0);
                    var state = new Vector(n);
                    for (int j = 0; j < n; ++j)
                    {
                        state[j] = path[j, i];
                    }

                    states[i] = state;
                }
            }
        }
    }
}
