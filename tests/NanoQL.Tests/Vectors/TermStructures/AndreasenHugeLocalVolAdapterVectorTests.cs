// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AndreasenHugeLocalVolAdapter vectors,
/// <c>tests/vectors/termstructures/volatility/equityfx/andreasenhugelocalvoladapter.json</c>: the local volatility
/// structure of six calibrations, its dates and range, and its local volatilities, strikes off the grid clamped onto
/// it, at the points the stability filter kept.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AndreasenHugeLocalVolAdapterVectorTests
{
    [Fact]
    public void AndreasenHugeLocalVolAdapterVector()
    {
        var file = VectorFile.Load("termstructures/volatility/equityfx/andreasenhugelocalvoladapter");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var a = new AndreasenHugeLocalVolAdapter(
                AndreasenHugeVolatilityInterplVectorTests.InterplOf(c.Input("calibration"), c.Context().EvaluationDate));
            double[][] points = [.. c.Input("points").EnumerateArray().Select(Doubles)];
            check.Output(c, "referenceDate", a.ReferenceDate);
            check.Output(c, "maxDate", a.MaxDate);
            check.Output(c, "minStrike", a.MinStrike());
            check.Output(c, "maxStrike", a.MaxStrike());
            check.Output(c, "dayCounter", a.DayCounter.Name);
            check.Output(c, "localVol", (object?[])[.. points.Select(p => Lazy(() => a.LocalVol(p[0], p[1], true)))]);
        }

        check.Complete();
    }
}
