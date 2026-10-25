// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmCEVOp vectors, <c>tests/vectors/methods/finitedifferences/operators/fdmcevop.json</c>: the operator in
/// direction 0 on three CEV processes, on the engine's mesher and as the first of two directions, at three times,
/// applied, applied in parts and solved by splitting on seeded vectors.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmCEVOpVectorTests
{
    [Fact]
    public void FdmCEVOpVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/operators/fdmcevop");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            FdmMesherComposite mesher = MesherOf(c.Input("mesher"), c.Context());
            FdmLinearOpComposite op = new FdmCEVOp(
                mesher, CurveOf(c.Input("rTS")), c.Double("f0"), c.Double("alpha"), c.Double("beta"), c.Int("direction"));
            ShortRateFdInputs.CheckOperator(check, c, op, mesher.Layout.Dim.Length);
        }

        check.Complete();
    }
}
