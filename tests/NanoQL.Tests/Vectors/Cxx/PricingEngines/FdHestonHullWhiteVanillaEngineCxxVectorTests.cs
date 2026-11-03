// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's FD Heston-Hull-White vectors, <c>tests/vectors/cxx/pricingengines/vanilla/fdhestonhullwhitevanillaengine.json</c>:
/// <see cref="FdHestonHullWhiteVanillaEngine.CalculateStrikes(VanillaOption, IEnumerable{double}, EvaluationContext,
/// HestonModel, HullWhiteProcess, double, int, int, int, int, int, bool, FdmSchemeDesc?)"/> against the results QuantLib's
/// engine caches for each strike, the reference strike at the spot and away from it, calls and puts, with and without the
/// control variate.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdHestonHullWhiteVanillaEngineCxxVectorTests
{
    [Fact]
    public void FdHestonHullWhiteVanillaEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/vanilla/fdhestonhullwhitevanillaengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            HestonProcess process = HestonCxxInputs.ProcessOf(c.Input("process"));
            var model = new HestonModel(process);
            var hullWhite = new HullWhiteProcess(process.RiskFreeRate, c.Double("a"), c.Double("sigma"));
            JsonElement exercise = c.Input("exercise");
            var date = new Date(exercise[1].GetInt32());
            Exercise ex = exercise[0].GetString() == "American"
                ? new AmericanExercise(context.EvaluationDate, date)
                : new EuropeanExercise(date);
            var type = (OptionType)c.Int("optionType");
            JsonElement scheme = c.Input("scheme");
            var reference = new VanillaOption(new PlainVanillaPayoff(type, c.Double("referenceStrike")), ex);
            var strikes = new Lazy<ImmutableArray<OneAssetOptionResults>>(() => FdHestonHullWhiteVanillaEngine.CalculateStrikes(
                reference,
                Doubles(c.Input("strikes")),
                context,
                model,
                hullWhite,
                c.Double("correlation"),
                c.Int("tGrid"),
                c.Int("xGrid"),
                c.Int("vGrid"),
                c.Int("rGrid"),
                c.Int("dampingSteps"),
                c.Input("controlVariate").GetBoolean(),
                new FdmSchemeDesc((FdmSchemeDescFdmSchemeType)scheme[0].GetInt32(), scheme[1].GetDouble(), scheme[2].GetDouble())));
            check.Output(c, "NPV", () => strikes.Value.Select(r => r.NPV).ToArray());
            check.Output(c, "delta", () => strikes.Value.Select(r => r.Delta).ToArray());
            check.Output(c, "gamma", () => strikes.Value.Select(r => r.Gamma).ToArray());
            check.Output(c, "theta", () => strikes.Value.Select(r => r.Theta).ToArray());
        }

        check.Complete();
    }
}
