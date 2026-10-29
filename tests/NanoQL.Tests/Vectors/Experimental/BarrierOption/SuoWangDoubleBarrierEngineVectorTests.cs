// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.BarrierInputs;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The SuoWangDoubleBarrierEngine vectors, <c>tests/vectors/experimental/barrieroption/suowangdoublebarrierengine.json</c>:
/// double knock-ins and knock-outs on three corridors over three expiries on ten processes, calls and puts at three
/// strikes inside the corridor; an expired option and the refusals. The outputs nanoql fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SuoWangDoubleBarrierEngineVectorTests
{
    [Fact]
    public void SuoWangDoubleBarrierEngineVector()
    {
        var file = VectorFile.Load("experimental/barrieroption/suowangdoublebarrierengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            int series = c.Int("series");
            check.Output(
                c,
                "NPV",
                c.Input("payoffs").EnumerateArray()
                    .Select(p => Lazy(() => SuoWangDoubleBarrierEngine.Calculate(
                        DoubleOptionOf(c.Input("barrier"), p, c.Input("exercise"), context), context, process, series).NPV))
                    .ToArray());
        }

        check.Complete();
    }
}
