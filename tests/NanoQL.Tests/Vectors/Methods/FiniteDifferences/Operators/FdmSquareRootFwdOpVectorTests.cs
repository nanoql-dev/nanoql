// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmSquareRootFwdOp vectors, <c>tests/vectors/methods/finitedifferences/operators/fdmsquarerootfwdop.json</c>:
/// <see cref="FdmSquareRootFwdOp"/> after <see cref="FdmSquareRootFwdOp.SetTime"/> at three time steps, on four
/// parameter sets under the plain, power and log transformations, on uniform, predefined and two-dimensional meshers.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmSquareRootFwdOpVectorTests
{
    [Fact]
    public void FdmSquareRootFwdOpVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/operators/fdmsquarerootfwdop");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("op");
            FdmMesherComposite mesher = MesherOf(spec.GetProperty("mesher"), c.Context());
            int direction = spec.GetProperty("direction").GetInt32();
            var op = new FdmSquareRootFwdOp(
                mesher,
                spec.GetProperty("kappa").GetDouble(),
                spec.GetProperty("theta").GetDouble(),
                spec.GetProperty("sigma").GetDouble(),
                direction,
                (FdmSquareRootFwdOpTransformationType)spec.GetProperty("type").GetInt32());
            FdmBlackScholesFwdOpVectorTests.CheckOperator(check, c, op, mesher, direction);
        }

        check.Complete();
    }
}
