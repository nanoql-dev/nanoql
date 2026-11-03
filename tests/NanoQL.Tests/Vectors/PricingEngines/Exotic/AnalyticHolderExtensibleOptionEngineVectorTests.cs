// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AnalyticHolderExtensibleOptionEngine vectors, <c>tests/vectors/pricingengines/exotic/analyticholderextensibleoptionengine.json</c>:
/// <see cref="AnalyticHolderExtensibleOptionEngine"/>'s NPV for holder-extensible calls on flat markets without
/// dividends, within Drezner's accuracy of QuantLib's.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticHolderExtensibleOptionEngineVectorTests
{
    [Fact]
    public void AnalyticHolderExtensibleOptionEngineVector()
    {
        var file = VectorFile.Load("pricingengines/exotic/analyticholderextensibleoptionengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement o = c.Input("option");
            var option = new HolderExtensibleOption(
                (OptionType)o.GetProperty("type").GetInt32(),
                o.GetProperty("premium").GetDouble(),
                DateOf(o.GetProperty("secondExpiryDate")),
                o.GetProperty("secondStrike").GetDouble(),
                PayoffOf(o.GetProperty("payoff")),
                ExerciseOf(o.GetProperty("exercise"), context));
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            check.Output(c, "NPV", () => AnalyticHolderExtensibleOptionEngine.Calculate(option, context, process).NPV);
        }

        check.Complete();
    }

    private static Date DateOf(JsonElement spec) => new(spec.GetInt32());
}
