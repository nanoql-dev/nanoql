// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmSabrOp vectors, <c>tests/vectors/methods/finitedifferences/operators/fdmsabrop.json</c>: the operator on three
/// SABR models, on the engine's mesher of the forward by the log volatility, at three times, applied, applied in parts
/// and solved by splitting on seeded vectors.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmSabrOpVectorTests
{
    [Fact]
    public void FdmSabrOpVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/operators/fdmsabrop");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            FdmMesherComposite mesher = MesherOf(c.Input("mesher"), c.Context());
            FdmLinearOpComposite op = new FdmSabrOp(
                mesher, CurveOf(c.Input("rTS")), c.Double("f0"), c.Double("alpha"), c.Double("beta"), c.Double("nu"), c.Double("rho"));
            ShortRateFdInputs.CheckOperator(check, c, op, mesher.Layout.Dim.Length);
        }

        check.Complete();
    }
}
