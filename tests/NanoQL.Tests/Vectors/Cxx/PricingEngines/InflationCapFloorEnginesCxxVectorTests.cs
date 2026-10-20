// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's year-on-year cap and floor engine vectors,
/// <c>tests/vectors/cxx/pricingengines/inflation/inflationcapfloorengines.json</c>: each engine's NPV and its
/// additional results <c>optionletsPrice</c>, <c>optionletsAtmForward</c> and <c>optionletsStdDev</c>, the last absent
/// for a collar.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InflationCapFloorEnginesCxxVectorTests
{
    [Fact]
    public void InflationCapFloorEnginesCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/inflation/inflationcapfloorengines");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            (InflationIndex index, EvaluationContext context) = InflationCashFlowInputs.IndexOf(c);
            var yii = (YoYInflationIndex)index;
            YoYInflationCapFloor capFloor = InflationCapFloorVectorTests.CapFloorOf(c.Input("instrument"), yii, context);
            InstrumentResults results =
                InflationCapFloorVectorTests.EngineOf(c.Input("engine"), capFloor, yii, c.Input("nominalCurve"), context);
            check.Output(c, "NPV", results.NPV);
            foreach (string tag in (string[])["optionletsPrice", "optionletsAtmForward", "optionletsStdDev"])
            {
                check.Output(c, tag, results.AdditionalResults.TryGetValue(tag, out object? value) ? value : null);
            }
        }

        check.Complete();
    }
}
