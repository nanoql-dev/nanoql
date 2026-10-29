// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Quotes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The VannaVolgaDoubleBarrierEngine vectors,
/// <c>tests/vectors/experimental/barrieroption/vannavolgadoublebarrierengine.json</c>: FX double barriers on two smiles,
/// two corridors, one and two years and two equal rate pairs, over the analytic and Suo-Wang engines, with and without
/// the vanilla-delta adaptation; the spot beyond each barrier, an expired option and a refused KIKO. The outputs nanoql
/// fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class VannaVolgaDoubleBarrierEngineVectorTests
{
    [Fact]
    public void VannaVolgaDoubleBarrierEngineVector()
    {
        var file = VectorFile.Load("experimental/barrieroption/vannavolgadoublebarrierengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Date today = context.EvaluationDate;
            JsonElement q = c.Input("quotes");
            double t = q.GetProperty("maturity").GetDouble();
            var atm = new DeltaVolQuote(
                q.GetProperty("atm").GetDouble(), DeltaVolQuoteDeltaType.Fwd, t, DeltaVolQuoteAtmType.AtmDeltaNeutral);
            var put25 = new DeltaVolQuote(-0.25, q.GetProperty("put25").GetDouble(), t, DeltaVolQuoteDeltaType.Fwd);
            var call25 = new DeltaVolQuote(0.25, q.GetProperty("call25").GetDouble(), t, DeltaVolQuoteDeltaType.Fwd);
            JsonElement rates = c.Input("rates");
            DayCounter dc = new Actual365Fixed();
            var domestic = new FlatForward(today, rates.GetProperty("domestic").GetDouble(), dc);
            var foreign = new FlatForward(today, rates.GetProperty("foreign").GetDouble(), dc);
            JsonElement b = c.Input("barrier");
            var exercise = new EuropeanExercise(new Date(c.Int("exerciseDate")));
            bool adapt = c.Bool("adaptVanDelta");
            double bs = c.Double("bsPriceWithSmile");
            double spot = c.Double("spot");
            Func<DoubleBarrierOption, EvaluationContext, GeneralizedBlackScholesProcess, int, OneAssetOptionResults> inner =
                c.String("inner") == "analytic" ? AnalyticDoubleBarrierEngine.Calculate : SuoWangDoubleBarrierEngine.Calculate;
            check.Output(
                c,
                "NPV",
                c.Input("payoffs").EnumerateArray()
                    .Select(p => Lazy(() => VannaVolgaDoubleBarrierEngine.Calculate(
                        new DoubleBarrierOption(
                            (DoubleBarrierType)b.GetProperty("barrierType").GetInt32(),
                            b.GetProperty("barrierLo").GetDouble(),
                            b.GetProperty("barrierHi").GetDouble(),
                            b.GetProperty("rebate").GetDouble(),
                            PayoffOf(p),
                            exercise),
                        context,
                        inner,
                        atm,
                        put25,
                        call25,
                        spot,
                        domestic,
                        foreign,
                        adapt,
                        bs).NPV))
                    .ToArray());
        }

        check.Complete();
    }
}
