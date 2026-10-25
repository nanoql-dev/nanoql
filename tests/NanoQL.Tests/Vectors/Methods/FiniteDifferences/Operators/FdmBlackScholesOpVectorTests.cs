// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmBlackScholesOp vectors, <c>tests/vectors/methods/finitedifferences/operators/fdmblackscholesop.json</c>:
/// <see cref="FdmBlackScholesOp"/> after <see cref="FdmBlackScholesOp.SetTime"/> at three time steps, over six
/// processes, with the Black and the local volatility, with and without a quanto helper, in one and two dimensions: its
/// apply, directional apply, mixed apply, splitting solve and preconditioner on a seeded vector, and its sparse
/// matrix's stored elements (<see cref="FdmLinearOpComposite.ToMatrix"/>).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmBlackScholesOpVectorTests
{
    [Fact]
    public void FdmBlackScholesOpVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/operators/fdmblackscholesop");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            (FdmBlackScholesOp op, FdmMesherComposite mesher) = OperatorOf(c.Input("op"), c.Context());
            int d = c.Input("op").GetProperty("direction").GetInt32();
            bool two = mesher.Layout.Dim.Length == 2;
            Vector r = VectorOf(c.Input("r"));
            double s = c.Double("s");
            var outputs = new Dictionary<string, List<object?>>
            {
                ["apply"] = [], ["applyDirection"] = [], ["applyOther"] = [], ["applyMixed"] = [], ["solveSplitting"] = [],
                ["solveOther"] = [], ["preconditioner"] = [], ["matrixRows"] = [], ["matrixColumns"] = [], ["matrixValues"] = [],
            };
            foreach (var t in c.Input("times").EnumerateArray())
            {
                op.SetTime(t[0].GetDouble(), t[1].GetDouble());
                outputs["apply"].Add(op.Apply(r).ToArray());
                outputs["applyDirection"].Add(op.ApplyDirection(d, r).ToArray());
                outputs["applyMixed"].Add(op.ApplyMixed(r).ToArray());
                outputs["solveSplitting"].Add(op.SolveSplitting(d, r, s).ToArray());
                outputs["preconditioner"].Add(op.Preconditioner(r, s).ToArray());
                (int[] rows, int[] columns, double[] values) = Stored(op.ToMatrix());
                outputs["matrixRows"].Add(rows);
                outputs["matrixColumns"].Add(columns);
                outputs["matrixValues"].Add(values);
                if (two)
                {
                    outputs["applyOther"].Add(op.ApplyDirection(1 - d, r).ToArray());
                    outputs["solveOther"].Add(op.SolveSplitting(1 - d, r, s).ToArray());
                }
            }

            foreach ((string name, List<object?> values) in outputs)
            {
                if (two || (name != "applyOther" && name != "solveOther"))
                {
                    check.Output(c, name, values);
                }
            }
        }

        check.Complete();
    }

    // A sparse matrix's stored elements in storage order, as the wheel's row_idx, col_idx and data list them.
    internal static (int[] Rows, int[] Columns, double[] Values) Stored(SparseMatrix m)
    {
        var rows = new List<int>();
        for (int i = 0; i + 1 < m.Filled1; ++i)
        {
            for (int k = m.Index1Data[i]; k < m.Index1Data[i + 1]; ++k)
            {
                rows.Add(i);
            }
        }

        return ([.. rows], m.Index2Data[..rows.Count].ToArray(), m.ValueData[..rows.Count].ToArray());
    }
}
