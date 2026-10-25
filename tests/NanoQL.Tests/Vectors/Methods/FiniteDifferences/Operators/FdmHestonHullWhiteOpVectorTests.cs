// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmHestonHullWhiteOp vectors, <c>tests/vectors/methods/finitedifferences/operators/fdmhestonhullwhiteop.json</c>:
/// the operator on Heston processes with Hull-White processes and equity-rate correlations, at three times, applied,
/// applied in parts and solved by splitting on seeded vectors, and its sparse matrix at the second; and the refusal of
/// correlations whose squares sum past 1.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmHestonHullWhiteOpVectorTests
{
    [Fact]
    public void FdmHestonHullWhiteOpVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/operators/fdmhestonhullwhiteop");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            FdmMesherComposite mesher = MesherOf(c.Input("mesher"), context);
            HestonProcess process = HestonProcessVectorTests.HestonProcessOf(c.Input("process"));
            HullWhiteProcess hullWhite = HullWhiteProcessOf(c.Input("hullWhite"), process);
            double correlation = c.Input("correlation").GetDouble();
            if (!c.Inputs.TryGetProperty("r", out _))
            {
                check.Output(c, "size", () => new FdmHestonHullWhiteOp(mesher, process, hullWhite, correlation).Size);
                continue;
            }

            var op = new FdmHestonHullWhiteOp(mesher, process, hullWhite, correlation);
            ShortRateFdInputs.CheckOperator(check, c, op, mesher.Layout.Dim.Length);

            // QuantLib's toMatrix, the sum of the decomposition's parts, at the second times
            JsonElement times = c.Input("times")[1];
            op.SetTime(times[0].GetDouble(), times[1].GetDouble());
            (int[] rows, int[] columns, double[] values) = FdmBlackScholesOpVectorTests.Stored(op.ToMatrix());
            check.Output(c, "matrixRows", new object?[] { rows });
            check.Output(c, "matrixColumns", new object?[] { columns });
            check.Output(c, "matrixValues", new object?[] { values });
        }

        check.Complete();
    }

    // A Hull-White spec {"a", "sigma"} on the Heston process's risk-free curve.
    internal static HullWhiteProcess HullWhiteProcessOf(JsonElement spec, HestonProcess process) =>
        new(process.RiskFreeRate, spec.GetProperty("a").GetDouble(), spec.GetProperty("sigma").GetDouble());
}
