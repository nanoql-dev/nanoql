// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The year-on-year inflation cap and floor engine vectors,
/// <c>tests/vectors/pricingengines/inflation/inflationcapfloorengines.json</c>:
/// <see cref="YoYInflationBlackCapFloorEngine"/>,
/// <see cref="YoYInflationUnitDisplacedBlackCapFloorEngine"/> and <see cref="YoYInflationBachelierCapFloorEngine"/>
/// across strikes, volatilities and surfaces: the NPV and the optionlets' prices.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InflationCapFloorEnginesVectorTests
{
    [Fact]
    public void InflationCapFloorEnginesVector()
    {
        var file = VectorFile.Load("pricingengines/inflation/inflationcapfloorengines");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            (InflationIndex index, EvaluationContext context) = InflationCashFlowInputs.IndexOf(c);
            var yii = (YoYInflationIndex)index;
            YoYInflationCapFloor capFloor = InflationCapFloorVectorTests.CapFloorOf(c.Input("instrument"), yii, context);
            InstrumentResults results =
                InflationCapFloorVectorTests.EngineOf(c.Input("engine"), capFloor, yii, c.Input("nominalCurve"), context);
            check.Output(c, "NPV", results.NPV);
            check.Output(c, "optionletPrices", results.Result<ImmutableArray<double>>("optionletsPrice"));
        }

        check.Complete();
    }
}
