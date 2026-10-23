// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The NinePointLinearOp vectors, <c>tests/vectors/methods/finitedifferences/operators/ninepointlinearop.json</c>: the
/// operator's apply through the mixed second derivative, its mult through the G2 operator's mixed term (the mixed
/// derivative multiplied by rho·sigma·eta, which QuantLib's FdmG2Op applies), and the constructor's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class NinePointLinearOpVectorTests
{
    [Fact]
    public void NinePointLinearOpVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/operators/ninepointlinearop");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            FdmMesherComposite mesher = MesherOf(c.Input("mesher"), c.Context());
            SecondOrderMixedDerivativeOpVectorTests.JsonDirections(c, out int d0, out int d1);
            if (c.Expected.TryGetProperty("construct", out _))
            {
                check.Output(c, "construct", () => new NinePointLinearOp(d0, d1, mesher));
                continue;
            }

            var model = (G2)ShortRateFdInputs.ModelOf(c.Input("model"));
            NinePointLinearOp mixed = new SecondOrderMixedDerivativeOp(d0, d1, mesher);
            NinePointLinearOp mult = mixed.Mult(new Vector(mesher.Layout.Size, model.Rho * model.Sigma * model.Eta));
            Vector[] rs = [.. c.Input("r").EnumerateArray().Select(VectorOf)];
            check.Output(c, "apply", rs.Select(r => (object?)mixed.Apply(r).ToArray()).ToArray());
            check.Output(c, "mult", rs.Select(r => (object?)mult.Apply(r).ToArray()).ToArray());
        }

        check.Complete();
    }
}
