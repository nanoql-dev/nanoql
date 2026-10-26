// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AnalyticContinuousPartialFloatingLookbackEngine vectors,
/// <c>tests/vectors/pricingengines/lookback/analyticcontinuouspartialfloatinglookback.json</c>: calls and puts at two
/// running extrema and two lambdas each, the lookback period ending at three dates, over three expiries on ten
/// processes; an expired option. The outputs nanoql fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticContinuousPartialFloatingLookbackEngineVectorTests
{
    [Fact]
    public void AnalyticContinuousPartialFloatingLookbackEngineVector()
    {
        var file = VectorFile.Load("pricingengines/lookback/analyticcontinuouspartialfloatinglookback");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            var payoff = new FloatingTypePayoff((OptionType)c.Int("optionType"));
            double lambda = c.Double("lambda");
            check.Output(
                c,
                "NPV",
                c.Input("minmaxes").EnumerateArray()
                    .Select(m => Lazy(() => AnalyticContinuousPartialFloatingLookbackEngine.Calculate(
                        new ContinuousPartialFloatingLookbackOption(
                            m.GetDouble(), lambda, c.DateInput("lookbackPeriodEnd"), payoff, ExerciseOf(c.Input("exercise"), context)),
                        context,
                        process).NPV))
                    .ToArray());
        }

        check.Complete();
    }
}
