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
/// The FdmBlackScholesFwdOp vectors, <c>tests/vectors/methods/finitedifferences/operators/fdmblackscholesfwdop.json</c>:
/// <see cref="FdmBlackScholesFwdOp"/> after <see cref="FdmBlackScholesFwdOp.SetTime"/> at three time steps, over six
/// processes, with the Black and the local volatility, in one and two dimensions: the operator outputs
/// <see cref="FdmBlackScholesOpVectorTests"/> reads.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmBlackScholesFwdOpVectorTests
{
    [Fact]
    public void FdmBlackScholesFwdOpVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/operators/fdmblackscholesfwdop");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("op");
            FdmMesherComposite mesher = MesherOf(spec.GetProperty("mesher"), c.Context());
            var op = new FdmBlackScholesFwdOp(
                mesher,
                ProcessOf(spec.GetProperty("process"), c.Context()),
                spec.GetProperty("strike").GetDouble(),
                spec.GetProperty("localVol").GetBoolean(),
                CouponInputs.NullableReal(spec.GetProperty("illegalLocalVolOverwrite")),
                spec.GetProperty("direction").GetInt32());
            CheckOperator(check, c, op, mesher, spec.GetProperty("direction").GetInt32());
        }

        check.Complete();
    }

    // The outputs of FdmBlackScholesOp's generator, for any one-direction operator on a one- or two-dimensional mesher.
    internal static void CheckOperator(VectorAssert check, VectorCase c, FdmLinearOpComposite op, FdmMesherComposite mesher, int d)
    {
        bool two = mesher.Layout.Dim.Length == 2;
        Vector r = VectorOf(c.Input("r"));
        double s = c.Double("s");
        var outputs = new Dictionary<string, List<object?>>
        {
            ["apply"] = [], ["applyDirection"] = [], ["applyOther"] = [], ["applyMixed"] = [], ["solveSplitting"] = [],
            ["solveOther"] = [], ["preconditioner"] = [], ["matrixRows"] = [], ["matrixColumns"] = [], ["matrixValues"] = [],
        };
        foreach (JsonElement t in c.Input("times").EnumerateArray())
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

    // A sparse matrix's stored elements in storage order, as the wheel's row_idx, col_idx and data list them.
    private static (int[] Rows, int[] Columns, double[] Values) Stored(SparseMatrix m)
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
