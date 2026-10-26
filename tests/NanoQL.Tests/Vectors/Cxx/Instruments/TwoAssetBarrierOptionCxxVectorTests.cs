// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's two-asset barrier option vectors, <c>tests/vectors/cxx/instruments/twoassetbarrieroption.json</c>:
/// <see cref="TwoAssetBarrierOption.IsExpired"/> around the evaluation date under both settings of
/// includeReferenceDateEvents, the validation of each barrier type and of a value outside the type (refused by nanoql's
/// constructor), and <see cref="TwoAssetBarrierOption.Triggered"/> below, on and above the barrier.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TwoAssetBarrierOptionCxxVectorTests
{
    [Fact]
    public void TwoAssetBarrierOptionCxxVector()
    {
        var file = VectorFile.Load("cxx/instruments/twoassetbarrieroption");
        var check = new VectorAssert(file);
        var call = new PlainVanillaPayoff(OptionType.Call, 100.0);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Inputs.TryGetProperty("exerciseDate", out _))
            {
                var option = new TwoAssetBarrierOption(BarrierType.DownOut, 90.0, call, new EuropeanExercise(c.DateInput("exerciseDate")));
                check.Output(c, "isExpired", option.IsExpired(context));
            }
            else if (c.Inputs.TryGetProperty("underlying", out _))
            {
                var option = new TwoAssetBarrierOption(
                    (BarrierType)c.Int("barrierType"), c.Double("barrier"), call, new EuropeanExercise(context.EvaluationDate + 365));
                check.Output(c, "triggered", option.Triggered(c.Double("underlying")));
            }
            else
            {
                check.Output(c, "valid", Lazy(() =>
                {
                    var option = new TwoAssetBarrierOption(
                        (BarrierType)c.Int("barrierType"), c.Double("barrier"), call, new EuropeanExercise(context.EvaluationDate + 365));
                    option.Validate();
                    return true;
                }));
            }
        }

        check.Complete();
    }
}
