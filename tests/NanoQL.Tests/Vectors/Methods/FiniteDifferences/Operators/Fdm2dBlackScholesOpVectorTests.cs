// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.BasketInputs;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Fdm2dBlackScholesOp vectors, <c>tests/vectors/methods/finitedifferences/operators/fdm2dblackscholesop.json</c>:
/// <see cref="Fdm2dBlackScholesOp"/> after <see cref="Fdm2dBlackScholesOp.SetTime"/> at three time steps, on flat and
/// zero curves, with the Black and the local volatility: its apply, mixed apply, directional applies, splitting solves
/// and preconditioner on a seeded vector, its sparse matrix's stored elements, and a third direction, refused.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Fdm2dBlackScholesOpVectorTests
{
    [Fact]
    public void Fdm2dBlackScholesOpVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/operators/fdm2dblackscholesop");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            (Fdm2dBlackScholesOp op, _) = OperatorOf(c.Input("op"), context);
            Vector r = VectorOf(c.Input("r"));
            double s = c.Double("s");
            var outputs = new Dictionary<string, List<object?>>
            {
                ["apply"] = [], ["applyMixed"] = [], ["applyDirection0"] = [], ["applyDirection1"] = [],
                ["solveSplitting0"] = [], ["solveSplitting1"] = [], ["preconditioner"] = [], ["matrixRows"] = [],
                ["matrixColumns"] = [], ["matrixValues"] = [],
            };
            foreach (JsonElement t in c.Input("times").EnumerateArray())
            {
                op.SetTime(t[0].GetDouble(), t[1].GetDouble());
                outputs["apply"].Add(op.Apply(r).ToArray());
                outputs["applyMixed"].Add(op.ApplyMixed(r).ToArray());
                for (int d = 0; d < 2; ++d)
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

            check.Output(c, "applyDirection2", () => op.ApplyDirection(2, r).ToArray());
        }

        check.Complete();
    }

    // The operator an op spec describes, and its mesher.
    private static (Fdm2dBlackScholesOp Op, FdmMesherComposite Mesher) OperatorOf(JsonElement spec, EvaluationContext context)
    {
        FdmMesherComposite mesher = MesherOf(spec.GetProperty("mesher"), context);
        ImmutableArray<GeneralizedBlackScholesProcess> p = ProcessesOf(spec.GetProperty("processes"), context);
        JsonElement overwrite = spec.GetProperty("illegalLocalVolOverwrite");
        var op = new Fdm2dBlackScholesOp(
            mesher,
            p[0],
            p[1],
            spec.GetProperty("correlation").GetDouble(),
            spec.GetProperty("maturity").GetDouble(),
            spec.GetProperty("localVol").GetBoolean(),
            overwrite.ValueKind == JsonValueKind.Null ? null : overwrite.GetDouble());
        return (op, mesher);
    }
}
