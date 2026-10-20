// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The LocalVolCurve vectors, <c>tests/vectors/termstructures/volatility/equityfx/localvolcurve.json</c>: the local
/// volatility curve of a Black variance curve, read through a Black-Scholes process's local volatility and diffusion.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LocalVolCurveVectorTests
{
    [Fact]
    public void LocalVolCurveVector()
    {
        var file = VectorFile.Load("termstructures/volatility/equityfx/localvolcurve");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            GeneralizedBlackScholesProcess p = ProcessOf(c.Input("process"), c.Context());
            LocalVolTermStructure lv = p.LocalVolatility;
            Assert.IsType<LocalVolCurve>(lv);
            double[][] points = [.. c.Input("points").EnumerateArray().Select(Doubles)];
            check.Output(c, "referenceDate", lv.ReferenceDate);
            check.Output(c, "maxDate", lv.MaxDate);
            check.Output(c, "minStrike", lv.MinStrike());
            check.Output(c, "maxStrike", lv.MaxStrike());
            check.Output(c, "dayCounter", lv.DayCounter.Name);
            check.Output(c, "localVol", points.Select(x => (object?)lv.LocalVol(x[0], x[1], true)).ToArray());
            check.Output(c, "diffusion", points.Select(x => (object?)p.Diffusion(x[0], x[1])).ToArray());
        }

        check.Complete();
    }
}
