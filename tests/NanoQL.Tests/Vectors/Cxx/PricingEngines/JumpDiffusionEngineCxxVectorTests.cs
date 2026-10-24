// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="JumpDiffusionEngine"/>'s harness vectors, <c>tests/vectors/cxx/pricingengines/vanilla/jumpdiffusionengine.json</c>:
/// European calls and puts on Merton-76 processes over flat forwards and a constant volatility, at two accuracies, their
/// NPVs and Greeks, and the refusal of too few iterations.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class JumpDiffusionEngineCxxVectorTests
{
    private static readonly (string Name, Func<OneAssetOptionResults, double?> Get)[] Results =
    [
        ("NPV", r => r.NPV), ("delta", r => r.Delta), ("gamma", r => r.Gamma), ("theta", r => r.Theta),
        ("vega", r => r.Vega), ("rho", r => r.Rho), ("dividendRho", r => r.DividendRho),
    ];

    [Fact]
    public void JumpDiffusionEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/vanilla/jumpdiffusionengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Date reference = c.DateInput("referenceDate");
            DayCounter dc = CashFlowInputs.DayCounterOf(c.Input("dayCounter"));
            var process = new Merton76Process(
                c.Double("spot"),
                new FlatForward(reference, c.Double("dividendYield"), dc),
                new FlatForward(reference, c.Double("riskFreeRate"), dc),
                new BlackConstantVol(reference, new NullCalendar(), c.Double("volatility"), dc),
                c.Double("jumpIntensity"),
                c.Double("logMeanJump"),
                c.Double("logJumpVolatility"));
            var option = new VanillaOption(
                new PlainVanillaPayoff((OptionType)c.Int("optionType"), c.Double("strike")),
                new EuropeanExercise(c.DateInput("exerciseDate")));
            var results = new Lazy<OneAssetOptionResults>(() => JumpDiffusionEngine.Calculate(
                option, context, process, c.Double("relativeAccuracy"), c.Int("maxIterations")));
            foreach ((string name, Func<OneAssetOptionResults, double?> get) in Results)
            {
                check.Output(c, name, Lazy(() => get(results.Value)));
            }
        }

        check.Complete();
    }
}
