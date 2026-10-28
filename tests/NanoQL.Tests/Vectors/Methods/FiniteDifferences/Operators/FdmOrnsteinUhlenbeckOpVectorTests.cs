// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmOrnsteinUhlenbeckOp vectors,
/// <c>tests/vectors/methods/finitedifferences/operators/fdmornsteinuhlenbeckop.json</c>:
/// <see cref="FdmOrnsteinUhlenbeckOp"/> after <see cref="FdmOrnsteinUhlenbeckOp.SetTime"/> at three time steps, in one
/// and two dimensions and each direction, discounted on a flat or a zero curve: its apply, mixed apply, directional
/// applies, splitting solves and preconditioner on a seeded vector, its sparse matrix's stored elements, and its size.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmOrnsteinUhlenbeckOpVectorTests
{
    [Fact]
    public void FdmOrnsteinUhlenbeckOpVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/operators/fdmornsteinuhlenbeckop");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("op");
            FdmMesherComposite mesher = MesherOf(spec.GetProperty("mesher"), c.Context());
            JsonElement p = spec.GetProperty("process");
            var op = new FdmOrnsteinUhlenbeckOp(
                mesher,
                new OrnsteinUhlenbeckProcess(
                    p.GetProperty("speed").GetDouble(), p.GetProperty("vol").GetDouble(), p.GetProperty("x0").GetDouble(),
                    p.GetProperty("level").GetDouble()),
                CurveOf(spec.GetProperty("curve")),
                spec.GetProperty("direction").GetInt32());
            int n = mesher.Layout.Dim.Length;
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

            check.Output(c, "size", op.Size);
        }

        check.Complete();
    }
}
