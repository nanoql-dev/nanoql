// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;
using static NanoQL.Tests.Vectors.ExoticInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The QuantoEuropeanEngine and QuantoForwardEuropeanEngine vectors,
/// <c>tests/vectors/pricingengines/quanto/quantoengine.json</c>: <see cref="QuantoEngine"/> over
/// <see cref="AnalyticEuropeanEngine"/> and <see cref="ForwardVanillaEngine"/>, the NPV, Greeks and quanto sensitivities
/// of quanto vanilla and forward-start options under flat volatilities.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class QuantoEngineVectorTests
{
    [Fact]
    public void QuantoEngineVector()
    {
        var file = VectorFile.Load("pricingengines/quanto/quantoengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement o = c.Input("option");
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            JsonElement q = c.Input("quanto");
            YieldTermStructure foreign = CurveOf(q.GetProperty("foreign"));
            BlackVolTermStructure fxVol = BlackVolOf(q.GetProperty("fxVolatility"), context);
            double rho = q.GetProperty("correlation").GetDouble();
            var r = new Lazy<QuantoOptionResults>(() => o.GetProperty("kind").GetString() == "Vanilla"
                ? QuantoEngine.Calculate(
                    new QuantoVanillaOption(PayoffOf(o.GetProperty("payoff")), ExerciseOf(o.GetProperty("exercise"), context)),
                    context, process, foreign, fxVol, rho, AnalyticEuropeanEngine.Calculate)
                : QuantoEngine.Calculate(
                    QuantoForwardOf(o, context), context, process, foreign, fxVol, rho,
                    (f, ctx, p) => ForwardVanillaEngine.Calculate(f, ctx, p, AnalyticEuropeanEngine.Calculate)));
            AnalyticCliquetEngineVectorTests.CheckGreeks(check, c, new Lazy<OneAssetOptionResults>(() => r.Value));
            foreach ((string name, Func<QuantoOptionResults, double?> read) in new (string, Func<QuantoOptionResults, double?>)[]
            {
                ("qvega", x => x.Qvega), ("qrho", x => x.Qrho), ("qlambda", x => x.Qlambda),
            })
            {
                if (c.Expected.TryGetProperty(name, out _))
                {
                    check.Output(c, name, () => read(r.Value));
                }
            }
        }

        check.Complete();
    }

    private static QuantoForwardVanillaOption QuantoForwardOf(JsonElement spec, EvaluationContext context)
    {
        ForwardVanillaOption f = ForwardOf(spec, context);
        return new QuantoForwardVanillaOption(f.Moneyness, f.ResetDate, (StrikedTypePayoff)f.Payoff!, f.Exercise);
    }
}
