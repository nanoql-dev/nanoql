// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.ArrayVectorTests;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="QdPlusAmericanEngine"/>'s harness vectors,
/// <c>tests/vectors/cxx/pricingengines/vanilla/qdplusamericanengine.json</c>: the put's exercise boundary at times to
/// expiry with the solver's evaluations, its Chebyshev interpolation across [-1, 1], by six engine settings on six
/// markets, and the limit at expiry.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class QdPlusAmericanEngineCxxVectorTests
{
    [Fact]
    public void QdPlusAmericanEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/vanilla/qdplusamericanengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Expected.TryGetProperty("xMax", out _))
            {
                check.Output(
                    c,
                    "xMax",
                    c.Input("xMax").EnumerateArray().Select(Numbers)
                        .Select(row => Lazy(() => QdPlusAmericanEngine.XMax(row[0], row[1], row[2]))).ToArray());
                continue;
            }

            double[] m = Numbers(c.Input("market"));
            JsonElement e = c.Input("engine");
            Date today = c.Context().EvaluationDate;
            var placeholder = new BlackScholesMertonProcess(
                100.0,
                new FlatForward(today, 0.0, new Actual365Fixed()),
                new FlatForward(today, 0.0, new Actual365Fixed()),
                new BlackConstantVol(today, new NullCalendar(), 0.2, new Actual365Fixed()));
            var engine = new QdPlusAmericanEngine(
                placeholder,
                e[0].GetInt32(),
                (QdPlusAmericanEngineSolverType)e[1].GetInt32(),
                e[2].GetDouble(),
                e[3].ValueKind == JsonValueKind.Null ? null : e[3].GetInt32());
            check.Output(
                c,
                "atTau",
                Numbers(c.Input("taus")).Select(tau => Lazy(() =>
                {
                    (int evaluations, double boundary) = engine.PutExerciseBoundaryAtTau(m[0], m[1], m[2], m[3], m[4], m[5], tau);
                    return new object[] { evaluations, boundary };
                })).ToArray());
            check.Output(c, "boundary", () =>
            {
                var boundary = engine.GetPutExerciseBoundary(m[0], m[1], m[2], m[3], m[4], m[5]);
                return Numbers(c.Input("points")).Select(z => boundary.Evaluate(z)).ToArray();
            });
        }

        check.Complete();
    }
}
