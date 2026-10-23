// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The SecondOrderMixedDerivativeOp vectors,
/// <c>tests/vectors/methods/finitedifferences/operators/secondordermixedderivativeop.json</c>: the mixed derivative on
/// two- and three-direction meshers in each ordered pair of directions, applied to seeded vectors.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SecondOrderMixedDerivativeOpVectorTests
{
    [Fact]
    public void SecondOrderMixedDerivativeOpVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/operators/secondordermixedderivativeop");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            FdmMesherComposite mesher = MesherOf(c.Input("mesher"), c.Context());
            JsonDirections(c, out int d0, out int d1);
            var op = new SecondOrderMixedDerivativeOp(d0, d1, mesher);
            check.Output(c, "apply", c.Input("r").EnumerateArray().Select(r => (object?)op.Apply(VectorOf(r)).ToArray()).ToArray());
        }

        check.Complete();
    }

    internal static void JsonDirections(VectorCase c, out int d0, out int d1)
    {
        d0 = c.Input("directions")[0].GetInt32();
        d1 = c.Input("directions")[1].GetInt32();
    }
}
