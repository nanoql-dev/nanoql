// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmHullWhiteOp vectors, <c>tests/vectors/methods/finitedifferences/operators/fdmhullwhiteop.json</c>: the operator on
/// two models and three meshers, at three times, applied, applied in parts and solved by splitting on seeded vectors.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmHullWhiteOpVectorTests
{
    [Fact]
    public void FdmHullWhiteOpVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/operators/fdmhullwhiteop");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            FdmMesherComposite mesher = MesherOf(c.Input("mesher"), c.Context());
            int[] directions = [.. c.Input("directions").EnumerateArray().Select(d => d.GetInt32())];
            var model = (HullWhite)ShortRateFdInputs.ModelOf(c.Input("model"));
            FdmLinearOpComposite op = new FdmHullWhiteOp(mesher, model, directions[0]);
            ShortRateFdInputs.CheckOperator(check, c, op, mesher.Layout.Dim.Length);
        }

        check.Complete();
    }
}
