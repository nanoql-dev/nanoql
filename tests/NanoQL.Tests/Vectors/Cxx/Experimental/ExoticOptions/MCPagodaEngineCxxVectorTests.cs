// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

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
/// The C++ harness's pagoda vectors, <c>tests/vectors/cxx/experimental/exoticoptions/mcpagodaengine.json</c>:
/// <see cref="MCPagodaEngine"/>'s fixed-seed NPV, and its error estimate under the pseudo-random traits, through
/// <see cref="MakeMCPagodaEngine{TRng, TStats}"/> and on low-discrepancy paths; an expired option; the processes and
/// option as <c>tools/cxx/src/experimental/exoticoptions/mcpagodaengine.cpp</c> builds them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MCPagodaEngineCxxVectorTests
{
    [Fact]
    public void MCPagodaEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/exoticoptions/mcpagodaengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement e = c.Input("engine");
            Date today = context.EvaluationDate;
            StochasticProcessArray processes = ProcessesOf(
                e.GetProperty("processes"), e.GetProperty("correlation").GetDouble(), today);
            var option = new PagodaOption(
                e.GetProperty("fixingDays").EnumerateArray().Select(d => today + d.GetInt32()),
                e.GetProperty("roof").GetDouble(),
                e.GetProperty("fraction").GetDouble());
            bool antithetic = e.GetProperty("antithetic").GetBoolean();
            int samples = e.GetProperty("samples").GetInt32();
            long seed = e.GetProperty("seed").GetInt64();
            if (e.GetProperty("traits").GetString() == "pr")
            {
                var priced = new Lazy<MultiAssetOptionResults>(() => new MakeMCPagodaEngine<PseudoRandom, GeneralStatistics>(processes, seed)
                    .WithAntitheticVariate(antithetic)
                    .WithSamples(samples)
                    .ToEngine()(option, context));
                check.Output(c, "NPV", () => priced.Value.NPV);
                check.Output(c, "errorEstimate", () => priced.Value.ErrorEstimate);
            }
            else
            {
                check.Output(c, "NPV", () => MCPagodaEngine.Calculate<LowDiscrepancy, GeneralStatistics>(
                    option, context, processes, false, antithetic, samples, null, null, seed).NPV);
            }
        }

        check.Complete();
    }

    // The harness's processes: each [spot, r, q, volatility] on flat Act/365 (Fixed) forwards and a constant volatility
    // from the evaluation date, at one correlation off the diagonal
    internal static StochasticProcessArray ProcessesOf(JsonElement specs, double correlation, Date today)
    {
        DayCounter dc = new Actual365Fixed();
        StochasticProcess1D[] processes = [.. specs.EnumerateArray().Select(s => (StochasticProcess1D)new BlackScholesMertonProcess(
            s[0].GetDouble(),
            new FlatForward(today, s[2].GetDouble(), dc),
            new FlatForward(today, s[1].GetDouble(), dc),
            new BlackConstantVol(today, new NullCalendar(), s[3].GetDouble(), dc)))];
        var rho = new Matrix(processes.Length, processes.Length, correlation);
        for (int i = 0; i < processes.Length; ++i)
        {
            rho[i, i] = 1.0;
        }

        return new StochasticProcessArray([.. processes], rho);
    }
}
