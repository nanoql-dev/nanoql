// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's FD Heston engine vectors, <c>tests/vectors/cxx/pricingengines/vanilla/fdhestonvanillaengine.json</c>:
/// <see cref="MakeFdHestonVanillaEngine"/>'s engine after each setter, against QuantLib's builder given the same
/// calls; and <see cref="FdHestonVanillaEngine.CalculateStrikes(VanillaOption, IEnumerable{double}, EvaluationContext, HestonModel, int, int, int, int, FdmSchemeDesc?, LocalVolTermStructure?, double)"/>
/// against the results QuantLib's engine caches for each strike, the reference strike at the spot and away from it,
/// calls and puts; and the engine on high-carry markets against QuantLib's components under the widened-grid rule
/// (<c>Divergences/FdHestonHighCarryTests.cs</c>), the multi-strike solve there each strike alone.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdHestonVanillaEngineCxxVectorTests
{
    [Fact]
    public void FdHestonVanillaEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/vanilla/fdhestonvanillaengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement process = c.Input("process");
            var model = new HestonModel(HestonCxxInputs.ProcessOf(process));
            JsonElement exercise = c.Input("exercise");
            var date = new Date(exercise[1].GetInt32());
            Exercise ex = exercise[0].GetString() == "American"
                ? new AmericanExercise(context.EvaluationDate, date)
                : new EuropeanExercise(date);
            var type = (OptionType)c.Int("optionType");
            if (c.Input("kind").GetString() == "widened")
            {
                // the engine against QuantLib's components under the widened-grid rule (FdHestonHighCarryTests)
                var option = new VanillaOption(new PlainVanillaPayoff(type, c.Double("strike")), ex);
                ImmutableArray<Dividend> dividends =
                [
                    .. c.Input("dividendDates").EnumerateArray().Zip(Doubles(c.Input("dividendAmounts")))
                        .Select(p => (Dividend)new FixedDividend(p.Second, new Date(p.First.GetInt32()))),
                ];
                JsonElement s = c.Input("scheme");
                var results = new Lazy<OneAssetOptionResults>(() => FdHestonVanillaEngine.Calculate(
                    option,
                    context,
                    model,
                    dividends,
                    c.Int("tGrid"),
                    c.Int("xGrid"),
                    c.Int("vGrid"),
                    c.Int("dampingSteps"),
                    new FdmSchemeDesc((FdmSchemeDescFdmSchemeType)s[0].GetInt32(), s[1].GetDouble(), s[2].GetDouble())));
                check.Output(c, "NPV", () => results.Value.NPV);
                check.Output(c, "delta", () => results.Value.Delta);
                check.Output(c, "gamma", () => results.Value.Gamma);
                check.Output(c, "theta", () => results.Value.Theta);
                continue;
            }

            if (c.Input("kind").GetString() == "builder")
            {
                var option = new VanillaOption(new PlainVanillaPayoff(type, c.Double("strike")), ex);
                var results = new Lazy<OneAssetOptionResults>(() => Built(c, model, process)(option, context));
                check.Output(c, "NPV", () => results.Value.NPV);
                check.Output(c, "delta", () => results.Value.Delta);
                check.Output(c, "gamma", () => results.Value.Gamma);
                check.Output(c, "theta", () => results.Value.Theta);
                continue;
            }

            JsonElement scheme = c.Input("scheme");
            var reference = new VanillaOption(new PlainVanillaPayoff(type, c.Double("referenceStrike")), ex);
            var strikes = new Lazy<ImmutableArray<OneAssetOptionResults>>(() => FdHestonVanillaEngine.CalculateStrikes(
                reference,
                Doubles(c.Input("strikes")),
                context,
                model,
                c.Int("tGrid"),
                c.Int("xGrid"),
                c.Int("vGrid"),
                c.Int("dampingSteps"),
                new FdmSchemeDesc((FdmSchemeDescFdmSchemeType)scheme[0].GetInt32(), scheme[1].GetDouble(), scheme[2].GetDouble())));
            check.Output(c, "NPV", () => strikes.Value.Select(r => r.NPV).ToArray());
            check.Output(c, "delta", () => strikes.Value.Select(r => r.Delta).ToArray());
            check.Output(c, "gamma", () => strikes.Value.Select(r => r.Gamma).ToArray());
            check.Output(c, "theta", () => strikes.Value.Select(r => r.Theta).ToArray());
        }

        check.Complete();
    }

    // The engine the builder makes after a case's setters, in order.
    private static Func<VanillaOption, EvaluationContext, OneAssetOptionResults> Built(VectorCase c, HestonModel model, JsonElement process)
    {
        var make = new MakeFdHestonVanillaEngine(model);
        Date reference = new(process.GetProperty("referenceDate").GetInt32());
        DayCounter dc = CashFlowInputs.DayCounterOf(process.GetProperty("dayCounter"));
        foreach (JsonElement s in c.Input("setters").EnumerateArray())
        {
            make = s[0].GetString() switch
            {
                "withTGrid" => make.WithTGrid(s[1].GetInt32()),
                "withXGrid" => make.WithXGrid(s[1].GetInt32()),
                "withVGrid" => make.WithVGrid(s[1].GetInt32()),
                "withDampingSteps" => make.WithDampingSteps(s[1].GetInt32()),
                "withFdmSchemeDesc" => make.WithFdmSchemeDesc(new FdmSchemeDesc((FdmSchemeDescFdmSchemeType)s[1].GetInt32(), s[2].GetDouble(), s[3].GetDouble())),
                "withLeverageFunction" => make.WithLeverageFunction(new LocalConstantVol(reference, s[1].GetDouble(), dc)),
                "withQuantoHelper" => make.WithQuantoHelper(HestonCxxInputs.QuantoOf(s[1], process)),
                "withCashDividends" => make.WithCashDividends(
                    s[1].EnumerateArray().Select(d => new Date(d.GetInt32())), Doubles(s[2])),
                string name => throw new InvalidOperationException($"unknown setter {name}"),
                null => throw new InvalidOperationException("no setter"),
            };
        }

        return make.ToEngine();
    }
}
