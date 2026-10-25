// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmHestonFwdOp vectors, <c>tests/vectors/methods/finitedifferences/operators/fdmhestonfwdop.json</c>: the
/// forward operator on three Heston parameter sets under the plain, power and log transformations of the variance, on
/// zero curves, and with leverage functions, at three times, applied, applied in parts and solved by splitting on
/// seeded vectors, and its sparse matrix at the second.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmHestonFwdOpVectorTests
{
    [Fact]
    public void FdmHestonFwdOpVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/operators/fdmhestonfwdop");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            FdmMesherComposite mesher = MesherOf(c.Input("mesher"), c.Context());
            var op = new FdmHestonFwdOp(
                mesher,
                HestonProcessVectorTests.HestonProcessOf(c.Input("process")),
                (FdmSquareRootFwdOpTransformationType)c.Input("type").GetInt32(),
                FdHestonVanillaEngineVectorTests.LeverageOf(c.Input("leverage")));
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
}
