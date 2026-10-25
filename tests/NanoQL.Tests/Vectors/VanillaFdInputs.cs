// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The pricing specs of the CEV, SABR, shout and swing engines' generators (tools/vectors/gen/pricingengines/vanilla/
/// fdcevvanillaengine.py, fdsabrvanillaengine.py, fdblackscholesshoutengine.py and fdsimplebsswingengine.py), priced,
/// and their results compared.
/// </summary>
internal static class VanillaFdInputs
{
    /// <summary>Compares a pricing case's results, each the file expects, from one pricing.</summary>
    /// <param name="check">The file's assertion.</param>
    /// <param name="c">The case.</param>
    /// <param name="price">Prices the case's pricing spec.</param>
    public static void CheckPricing(
        VectorAssert check, VectorCase c, Func<JsonElement, EvaluationContext, OneAssetOptionResults> price)
    {
        EvaluationContext context = c.Context();
        var priced = new Lazy<OneAssetOptionResults>(() => price(c.Input("pricing"), context));
        (string Name, Func<OneAssetOptionResults, double?> Get)[] results =
        [
            ("NPV", r => r.NPV), ("delta", r => r.Delta), ("gamma", r => r.Gamma), ("theta", r => r.Theta),
        ];
        foreach ((string name, Func<OneAssetOptionResults, double?> get) in results)
        {
            if (c.Expected.TryGetProperty(name, out _))
            {
                check.Output(c, name, () => get(priced.Value));
            }
        }
    }

    /// <summary>The CEV engine's pricing of a spec.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The results.</returns>
    public static OneAssetOptionResults FdCev(JsonElement spec, EvaluationContext context) =>
        FdCEVVanillaEngine.Calculate(
            new VanillaOption(PayoffOf(spec.GetProperty("payoff")), FdExerciseOf(spec.GetProperty("exercise"), context)),
            context,
            Real(spec, "f0"),
            Real(spec, "alpha"),
            Real(spec, "beta"),
            CurveOf(spec.GetProperty("rTS")),
            Int(spec, "tGrid"),
            Int(spec, "xGrid"),
            Int(spec, "dampingSteps"),
            Real(spec, "scalingFactor"),
            Real(spec, "eps"),
            SchemeOf(spec.GetProperty("scheme")));

    /// <summary>The SABR engine's pricing of a spec.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The results.</returns>
    public static OneAssetOptionResults FdSabr(JsonElement spec, EvaluationContext context) =>
        FdSabrVanillaEngine.Calculate(
            new VanillaOption(PayoffOf(spec.GetProperty("payoff")), FdExerciseOf(spec.GetProperty("exercise"), context)),
            context,
            Real(spec, "f0"),
            Real(spec, "alpha"),
            Real(spec, "beta"),
            Real(spec, "nu"),
            Real(spec, "rho"),
            CurveOf(spec.GetProperty("rTS")),
            Int(spec, "tGrid"),
            Int(spec, "fGrid"),
            Int(spec, "xGrid"),
            Int(spec, "dampingSteps"),
            Real(spec, "scalingFactor"),
            Real(spec, "eps"),
            SchemeOf(spec.GetProperty("scheme")));

    /// <summary>The shout engine's pricing of a spec.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The results.</returns>
    public static OneAssetOptionResults FdShout(JsonElement spec, EvaluationContext context) =>
        FdBlackScholesShoutEngine.Calculate(
            new VanillaOption(PayoffOf(spec.GetProperty("payoff")), ShoutExerciseOf(spec.GetProperty("exercise"))),
            context,
            ProcessOf(spec.GetProperty("process"), context),
            DividendsOf(spec.GetProperty("dividends")),
            Int(spec, "tGrid"),
            Int(spec, "xGrid"),
            Int(spec, "dampingSteps"),
            SchemeOf(spec.GetProperty("scheme")));

    /// <summary>The swing engine's pricing of a spec.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The results.</returns>
    public static OneAssetOptionResults FdSwing(JsonElement spec, EvaluationContext context) =>
        FdSimpleBSSwingEngine.Calculate(
            SwingOptionOf(spec),
            context,
            ProcessOf(spec.GetProperty("process"), context),
            Int(spec, "tGrid"),
            Int(spec, "xGrid"),
            SchemeOf(spec.GetProperty("scheme")));

    /// <summary>The swing option a spec describes: its payoff, exercise dates and rights.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The option.</returns>
    public static VanillaSwingOption SwingOptionOf(JsonElement spec) =>
        new(
            SwingPayoffOf(spec.GetProperty("payoff")),
            new SwingExercise(spec.GetProperty("dates").EnumerateArray().Select(d => new Time.Date(d.GetInt32()))),
            Int(spec, "minExerciseRights"),
            Int(spec, "maxExerciseRights"));

    /// <summary>A plain-vanilla payoff, or the forward payoff <c>{"type": "Forward", "optionType", "strike"}</c>.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The payoff.</returns>
    public static StrikedTypePayoff SwingPayoffOf(JsonElement spec) =>
        spec.GetProperty("type").GetString() == "Forward"
            ? new VanillaForwardPayoff((OptionType)spec.GetProperty("optionType").GetInt32(), Real(spec, "strike"))
            : PayoffOf(spec);

    // the shout window: {"earliest", "latest"}
    private static Exercise ShoutExerciseOf(JsonElement spec) =>
        new AmericanExercise(new Time.Date(spec.GetProperty("earliest").GetInt32()), new Time.Date(spec.GetProperty("latest").GetInt32()));

    private static double Real(JsonElement spec, string name) => spec.GetProperty(name).GetDouble();

    private static int Int(JsonElement spec, string name) => spec.GetProperty(name).GetInt32();
}
