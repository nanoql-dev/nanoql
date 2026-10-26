// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmWienerOp vectors, <c>tests/vectors/methods/finitedifferences/operators/fdmwienerop.json</c>:
/// <see cref="FdmWienerOp"/> after <see cref="FdmWienerOp.SetTime"/> at three time steps, in one to three dimensions,
/// discounted on a flat or a zero curve or not at all: its apply, mixed apply, directional applies, splitting solves
/// and preconditioner on a seeded vector, its sparse matrix's stored elements, and rates of another dimension, refused.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmWienerOpVectorTests
{
    [Fact]
    public void FdmWienerOpVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/operators/fdmwienerop");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("op");
            if (c.Name.EndsWith("(refused)", StringComparison.Ordinal))
            {
                check.Output(c, "construction", () => OperatorOf(spec, context) is null ? 1.0 : 0.0);
                continue;
            }

            FdmWienerOp op = OperatorOf(spec, context);
            int n = spec.GetProperty("lambdas").GetArrayLength();
            Vector r = VectorOf(c.Input("r"));
            double s = c.Double("s");
            var outputs = new Dictionary<string, List<object?>>
            {
                ["apply"] = [], ["applyMixed"] = [], ["preconditioner"] = [], ["matrixRows"] = [], ["matrixColumns"] = [],
                ["matrixValues"] = [],
            };
            for (int d = 0; d < n; ++d)
            {
                outputs[$"applyDirection{d}"] = [];
                outputs[$"solveSplitting{d}"] = [];
            }

            foreach (JsonElement t in c.Input("times").EnumerateArray())
            {
                op.SetTime(t[0].GetDouble(), t[1].GetDouble());
                outputs["apply"].Add(op.Apply(r).ToArray());
                outputs["applyMixed"].Add(op.ApplyMixed(r).ToArray());
                for (int d = 0; d < n; ++d)
                {
                    outputs[$"applyDirection{d}"].Add(op.ApplyDirection(d, r).ToArray());
                    outputs[$"solveSplitting{d}"].Add(op.SolveSplitting(d, r, s).ToArray());
                }

                outputs["preconditioner"].Add(op.Preconditioner(r, s).ToArray());
                (int[] rows, int[] columns, double[] values) = FdmBlackScholesOpVectorTests.Stored(op.ToMatrix());
                outputs["matrixRows"].Add(rows);
                outputs["matrixColumns"].Add(columns);
                outputs["matrixValues"].Add(values);
            }

            foreach ((string name, List<object?> values) in outputs)
            {
                check.Output(c, name, values);
            }
        }

        check.Complete();
    }

    // The operator an op spec describes.
    private static FdmWienerOp OperatorOf(JsonElement spec, EvaluationContext context)
    {
        FdmMesherComposite mesher = MesherOf(spec.GetProperty("mesher"), context);
        JsonElement curve = spec.GetProperty("curve");
        return new FdmWienerOp(
            mesher, curve.ValueKind == JsonValueKind.Null ? null : CurveOf(curve), Doubles(spec.GetProperty("lambdas")));
    }
}
