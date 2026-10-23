// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Monte Carlo Hull-White cap/floor engine vectors,
/// <c>tests/vectors/cxx/pricingengines/capfloor/mchullwhiteengine.json</c>: <see cref="MCHullWhiteCapFloorEngine"/>,
/// which the wheel doesn't expose, on caps and floors, seasoned, fixing today and expired among them, with pseudo-random
/// and low-discrepancy traits, antithetic variates, a Brownian bridge, a number of samples or a tolerance, each with
/// its NPV and error estimate; and <see cref="MakeMCHullWhiteCapFloorEngine{TRng, TStats}"/>'s setters and refusals.
/// The collars are left out (<c>Divergences/McHullWhiteCollarTests.cs</c>).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MCHullWhiteCapFloorEngineCxxVectorTests
{
    [Fact]
    public void MCHullWhiteCapFloorEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/capfloor/mchullwhiteengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            YieldTermStructure curve = CashFlowInputs.CurveOf(c.Input("discountCurve"));
            HullWhite model = CalibrationInputs.HullWhiteOf(c.Input("hullWhite"), curve);
            CapFloor CapFloor() => CapFloorInputs.CapFloorOf(c.Input("capFloor"), context);
            if (c.Inputs.TryGetProperty("setters", out JsonElement setters))
            {
                bool pseudo = c.String("traits") == "PseudoRandom";
                check.Output(c, "NPV", () => (pseudo
                    ? Built<PseudoRandom>(model, setters)
                    : Built<LowDiscrepancy>(model, setters))(CapFloor(), context).NPV);
                continue;
            }

            string[] tags = [.. c.Input("tags").EnumerateArray().Select(t => t.GetString()!)];
            check.Output(c, "values", c.Input("engines").EnumerateArray().Select(e =>
            {
                InstrumentResults? results = null;
                Exception? failure = null;
                try
                {
                    results = Priced(CapFloor(), context, model, e);
                }
                catch (Exception x) when (x is ArgumentException or NanoQLException)
                {
                    failure = x;
                }

                return (object?)tags.Select(tag => (Func<object?>)(() => failure is not null ? throw failure : tag switch
                {
                    "NPV" => results!.NPV,
                    _ => results!.ErrorEstimate ?? throw new NanoQLException("error estimate not provided"),
                })).ToArray();
            }).ToArray());
        }

        check.Complete();
    }

    private static InstrumentResults Priced(CapFloor capFloor, EvaluationContext context, HullWhite model, JsonElement e)
    {
        bool bb = e.GetProperty("brownianBridge").GetBoolean(), anti = e.GetProperty("antitheticVariate").GetBoolean();
        int? samples = CouponInputs.NullableInt(e.GetProperty("requiredSamples"));
        double? tolerance = CouponInputs.NullableReal(e.GetProperty("requiredTolerance"));
        int? maxSamples = CouponInputs.NullableInt(e.GetProperty("maxSamples"));
        long seed = e.GetProperty("seed").GetInt64();
        return e.GetProperty("traits").GetString() == "PseudoRandom"
            ? MCHullWhiteCapFloorEngine.Calculate<PseudoRandom, GeneralStatistics>(capFloor, context, model, bb, anti, samples, tolerance, maxSamples, seed)
            : MCHullWhiteCapFloorEngine.Calculate<LowDiscrepancy, GeneralStatistics>(capFloor, context, model, bb, anti, samples, tolerance, maxSamples, seed);
    }

    // The builder on the first setter's seed, which every case gives first (QuantLib's default, 0, is refused), then
    // each setter in order.
    private static Func<CapFloor, EvaluationContext, InstrumentResults> Built<TRng>(HullWhite model, JsonElement setters)
        where TRng : IRngTraits
    {
        JsonElement[] calls = [.. setters.EnumerateArray()];
        var b = new MakeMCHullWhiteCapFloorEngine<TRng, GeneralStatistics>(model, calls[0][1].GetInt64());
        foreach (JsonElement call in calls)
        {
            JsonElement value = call[1];
            b = call[0].GetString() switch
            {
                "withSamples" => b.WithSamples(value.GetInt32()),
                "withAbsoluteTolerance" => b.WithAbsoluteTolerance(value.GetDouble()),
                "withMaxSamples" => b.WithMaxSamples(value.GetInt32()),
                "withSeed" => b.WithSeed(value.GetInt64()),
                "withBrownianBridge" => b.WithBrownianBridge(value.GetBoolean()),
                "withAntitheticVariate" => b.WithAntitheticVariate(value.GetBoolean()),
                string name => throw new InvalidOperationException($"unknown setter '{name}'"),
                null => throw new InvalidOperationException("a setter without a name"),
            };
        }

        return b.ToEngine();
    }
}
