// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The PerpetualFutures vectors, <c>tests/vectors/instruments/perpetualfutures.json</c>: linear and inverse futures
/// priced by <see cref="DiscountingPerpetualFuturesEngine"/>, funded discretely and continuously on one rate and on
/// interpolated terms, and the engine's checks of its lists.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PerpetualFuturesVectorTests
{
    [Fact]
    public void PerpetualFuturesVector()
    {
        var file = VectorFile.Load("instruments/perpetualfutures");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement f = c.Input("futures"), e = c.Input("engine");
            JsonElement frequency = f.GetProperty("fundingFrequency");
            var futures = new PerpetualFutures(
                (PerpetualFuturesPayoffType)f.GetProperty("payoffType").GetInt32(),
                (PerpetualFuturesFundingType)f.GetProperty("fundingType").GetInt32(),
                new Period(frequency.GetProperty("length").GetInt32(), (TimeUnit)frequency.GetProperty("units").GetInt32()),
                new NullCalendar(),
                CashFlowInputs.DayCounterOf(f.GetProperty("dayCounter")));
            check.Output(c, "NPV", () => DiscountingPerpetualFuturesEngine.Calculate(
                futures,
                context,
                CurveOf(e.GetProperty("domestic")),
                CurveOf(e.GetProperty("foreign")),
                e.GetProperty("spot").GetDouble(),
                Doubles(e.GetProperty("fundingTimes")),
                Doubles(e.GetProperty("fundingRates")),
                Doubles(e.GetProperty("interestRateDiffs")),
                (DiscountingPerpetualFuturesEngineInterpolationType)e.GetProperty("interpolation").GetInt32(),
                e.GetProperty("maxT").GetDouble()).NPV);
            check.Output(c, "isExpired", futures.IsExpired(context));
        }

        check.Complete();
    }
}
