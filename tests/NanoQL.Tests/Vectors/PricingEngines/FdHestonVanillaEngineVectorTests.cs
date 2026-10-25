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
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdHestonVanillaEngine vectors, <c>tests/vectors/pricingengines/vanilla/fdhestonvanillaengine.json</c>: the NPV,
/// delta, gamma and theta of European and American calls and puts on three Heston parameter sets, and on the first:
/// QuantLib's default grid, damping steps, every named scheme, a mixing factor, a cash-or-nothing payoff, cash
/// dividends, a quanto helper, leverage functions, and an expired option; each priced through the
/// <see cref="FdHestonVanillaEngine.Calculate(VanillaOption, EvaluationContext, HestonModel, int, int, int, int, FdmSchemeDesc?, LocalVolTermStructure?, double)"/>
/// overload QuantLib's constructor matches.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdHestonVanillaEngineVectorTests
{
    [Fact]
    public void FdHestonVanillaEngineVector()
    {
        var file = VectorFile.Load("pricingengines/vanilla/fdhestonvanillaengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            var priced = new Lazy<OneAssetOptionResults>(() => Price(c.Input("pricing"), context));
            (string Name, Func<OneAssetOptionResults, double?> Get)[] results =
            [
                ("NPV", r => r.NPV), ("delta", r => r.Delta), ("gamma", r => r.Gamma), ("theta", r => r.Theta),
            ];
            foreach ((string name, Func<OneAssetOptionResults, double?> get) in results)
            {
                check.Output(c, name, () => get(priced.Value));
            }
        }

        check.Complete();
    }

    /// <summary>The leverage function a spec describes: none, a constant local volatility or a fixed local volatility
    /// surface.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The leverage function.</returns>
    internal static LocalVolTermStructure? LeverageOf(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null
            ? null
            : spec.GetProperty("type").GetString() == "LocalConstantVol"
                ? new LocalConstantVol(
                    new Date(spec.GetProperty("referenceDate").GetInt32()),
                    spec.GetProperty("volatility").GetDouble(),
                    CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")))
                : FixedLocalVolSurfaceVectorTests.SurfaceOf(spec);

    private static OneAssetOptionResults Price(JsonElement spec, EvaluationContext context)
    {
        var model = new HestonModel(HestonProcessVectorTests.HestonProcessOf(spec.GetProperty("process")));
        var option = new VanillaOption(PayoffOf(spec.GetProperty("payoff")), FdExerciseOf(spec.GetProperty("exercise"), context));
        ImmutableArray<Dividend> dividends = DividendsOf(spec.GetProperty("dividends"));
        FdmQuantoHelper? quanto = QuantoOf(spec.GetProperty("quanto"), context);
        int tGrid = spec.GetProperty("tGrid").GetInt32();
        int xGrid = spec.GetProperty("xGrid").GetInt32();
        int vGrid = spec.GetProperty("vGrid").GetInt32();
        int damping = spec.GetProperty("dampingSteps").GetInt32();
        FdmSchemeDesc scheme = SchemeOf(spec.GetProperty("scheme"));
        LocalVolTermStructure? leverage = LeverageOf(spec.GetProperty("leverage"));
        double mixing = spec.GetProperty("mixingFactor").GetDouble();
        return (dividends.IsEmpty, quanto is null) switch
        {
            (true, true) => FdHestonVanillaEngine.Calculate(option, context, model, tGrid, xGrid, vGrid, damping, scheme, leverage, mixing),
            (false, true) => FdHestonVanillaEngine.Calculate(
                option, context, model, dividends, tGrid, xGrid, vGrid, damping, scheme, leverage, mixing),
            (true, false) => FdHestonVanillaEngine.Calculate(
                option, context, model, quanto, tGrid, xGrid, vGrid, damping, scheme, leverage, mixing),
            (false, false) => FdHestonVanillaEngine.Calculate(
                option, context, model, dividends, quanto, tGrid, xGrid, vGrid, damping, scheme, leverage, mixing),
        };
    }
}
