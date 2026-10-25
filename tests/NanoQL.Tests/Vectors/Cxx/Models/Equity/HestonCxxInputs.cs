// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's equity specs, as tools/cxx/src/models/equity/hestonspec.hpp reads them: Heston and Bates processes
/// on flat forwards, and quanto helpers.
/// </summary>
internal static class HestonCxxInputs
{
    /// <summary>
    /// The process a spec describes: a Bates process where it has <c>lambda</c>, <c>nu</c> and <c>delta</c>, a Heston
    /// process otherwise, each under QuantLib's default discretization on flat forwards at <c>riskFree</c> and
    /// <c>dividend</c> from <c>referenceDate</c> on <c>dayCounter</c>.
    /// </summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The process.</returns>
    public static HestonProcess ProcessOf(JsonElement spec)
    {
        YieldTermStructure riskFree = Curve(spec, "riskFree"), dividend = Curve(spec, "dividend");
        double s0 = Real(spec, "s0"), v0 = Real(spec, "v0"), kappa = Real(spec, "kappa"), theta = Real(spec, "theta");
        double sigma = Real(spec, "sigma"), rho = Real(spec, "rho");
        return spec.TryGetProperty("lambda", out JsonElement lambda)
            ? new BatesProcess(riskFree, dividend, s0, v0, kappa, theta, sigma, rho, lambda.GetDouble(), Real(spec, "nu"), Real(spec, "delta"))
            : new HestonProcess(riskFree, dividend, s0, v0, kappa, theta, sigma, rho);
    }

    /// <summary>The Bates process a spec with jumps describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The process.</returns>
    public static BatesProcess BatesProcessOf(JsonElement spec) => Assert.IsType<BatesProcess>(ProcessOf(spec));

    /// <summary>
    /// The quanto helper a spec describes, or <see langword="null"/> for a null spec: on the process's risk-free curve,
    /// a flat foreign curve and a constant FX volatility on the process's reference date and day counter.
    /// </summary>
    /// <param name="spec">The quanto spec.</param>
    /// <param name="process">The process spec.</param>
    /// <returns>The helper.</returns>
    public static FdmQuantoHelper? QuantoOf(JsonElement spec, JsonElement process)
    {
        if (spec.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        Date reference = new(process.GetProperty("referenceDate").GetInt32());
        DayCounter dc = CashFlowInputs.DayCounterOf(process.GetProperty("dayCounter"));
        return new FdmQuantoHelper(
            Curve(process, "riskFree"),
            new FlatForward(reference, Real(spec, "foreign"), dc),
            new BlackConstantVol(reference, new NullCalendar(), Real(spec, "fxVolatility"), dc),
            Real(spec, "correlation"),
            Real(spec, "atmLevel"));
    }

    private static FlatForward Curve(JsonElement spec, string rate) =>
        new(
            new Date(spec.GetProperty("referenceDate").GetInt32()),
            Real(spec, rate),
            CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")));

    private static double Real(JsonElement spec, string name) => spec.GetProperty(name).GetDouble();
}
