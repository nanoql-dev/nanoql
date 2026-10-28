// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The NthOrderDerivativeOp vectors,
/// <c>tests/vectors/methods/finitedifferences/operators/nthorderderivativeop.json</c>:
/// <see cref="NthOrderDerivativeOp"/>'s apply on a seeded vector, on uniform, concentrating and two-dimensional meshers
/// in each direction, for orders 0 to 3 on 2 to 7 points; and the numbers of points QuantLib refuses.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class NthOrderDerivativeOpVectorTests
{
    [Fact]
    public void NthOrderDerivativeOpVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/operators/nthorderderivativeop");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            FdmMesherComposite mesher = MesherOf(c.Input("mesher"), c.Context());
            Vector r = VectorOf(c.Input("r"));
            check.Output(
                c, "apply", () => new NthOrderDerivativeOp(c.Int("direction"), c.Int("order"), c.Int("nPoints"), mesher).Apply(r).ToArray());
        }

        check.Complete();
    }
}
