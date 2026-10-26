// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AnalyticContinuousFixedLookbackEngine vectors,
/// <c>tests/vectors/pricingengines/lookback/analyticcontinuousfixedlookback.json</c>: calls and puts at two running
/// extrema and three strikes each, over three expiries on ten processes; an expired option and the refusal of a
/// cash-or-nothing payoff. The outputs nanoql fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticContinuousFixedLookbackEngineVectorTests
{
    [Fact]
    public void AnalyticContinuousFixedLookbackEngineVector()
    {
        var file = VectorFile.Load("pricingengines/lookback/analyticcontinuousfixedlookback");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            double minmax = c.Double("minmax");
            check.Output(
                c,
                "NPV",
                c.Input("payoffs").EnumerateArray()
                    .Select(p => Lazy(() => AnalyticContinuousFixedLookbackEngine.Calculate(
                        new ContinuousFixedLookbackOption(minmax, PayoffOf(p), ExerciseOf(c.Input("exercise"), context)),
                        context,
                        process).NPV))
                    .ToArray());
        }

        check.Complete();
    }
}
