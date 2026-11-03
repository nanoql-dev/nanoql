// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The VanillaOption vectors, <c>tests/vectors/instruments/vanillaoption.json</c>: European options' expiry and expired
/// results, and implied volatilities with and without dividends, with the refusals, and those of American and Bermudan
/// options on the finite-difference engine. The implied volatilities whose solves a one-ulp move of their inputs moves
/// are left out of the file (the stability filter).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class VanillaOptionVectorTests
{
    private static readonly (string Name, Func<OneAssetOptionResults, double?> Get)[] Results =
    [
        ("delta", r => r.Delta), ("deltaForward", r => r.DeltaForward), ("elasticity", r => r.Elasticity),
        ("gamma", r => r.Gamma), ("theta", r => r.Theta), ("thetaPerDay", r => r.ThetaPerDay), ("vega", r => r.Vega),
        ("rho", r => r.Rho), ("dividendRho", r => r.DividendRho), ("strikeSensitivity", r => r.StrikeSensitivity),
        ("itmCashProbability", r => r.ItmCashProbability),
    ];

    [Fact]
    public void VanillaOptionVector()
    {
        var file = VectorFile.Load("instruments/vanillaoption");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            StrikedTypePayoff payoff = PayoffOf(c.Input("payoff"));
            Exercise exercise = ExerciseOf(c.Input("exercise"), context);
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            if (c.Expected.TryGetProperty("isExpired", out _))
            {
                var option = new EuropeanOption(payoff, exercise);
                check.Output(c, "isExpired", option.IsExpired(context));
                if (c.Expected.TryGetProperty("NPV", out _))
                {
                    OneAssetOptionResults r = AnalyticEuropeanEngine.Calculate(option, context, process);
                    check.Output(c, "NPV", r.NPV);
                    foreach ((string name, Func<OneAssetOptionResults, double?> get) in Results)
                    {
                        check.Output(c, name, get(r));
                    }
                }

                continue;
            }

            var vanilla = new VanillaOption(payoff, exercise);
            ImmutableArray<Dividend> dividends = DividendsOf(c.Input("dividends"));
            double target = !c.Bool("targetFrom")
                ? c.Expected.GetProperty("targetValue").GetDouble()
                : exercise.Type != ExerciseType.European

                    // the engine QuantLib's ImpliedVolatility builds for an early exercise, on its default grid
                    ? FdBlackScholesVanillaEngine.Calculate(vanilla, context, process, dividends).NPV!.Value
                    : (dividends.IsEmpty
                        ? AnalyticEuropeanEngine.Calculate(new EuropeanOption(payoff, exercise), context, process)
                        : AnalyticDividendEuropeanEngine.Calculate(new EuropeanOption(payoff, exercise), context, process, dividends)).NPV!.Value;
            check.Output(c, "targetValue", target);
            if (c.Expected.TryGetProperty("impliedVolatility", out _))
            {
                check.Output(
                    c,
                    "impliedVolatility",
                    Lazy(() => vanilla.ImpliedVolatility(
                        target,
                        process,
                        dividends,
                        context,
                        c.Double("accuracy"),
                        c.Int("maxEvaluations"),
                        c.Double("minVol"),
                        c.Double("maxVol"))));
            }
        }

        check.Complete();
    }
}
