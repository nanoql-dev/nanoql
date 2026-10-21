// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The TripleBandLinearOp vectors, <c>tests/vectors/methods/finitedifferences/operators/triplebandlinearop.json</c>:
/// <see cref="TripleBandLinearOp"/>'s <see cref="TripleBandLinearOp.Mult"/>, <see cref="TripleBandLinearOp.MultR"/>,
/// both <c>Add</c> overloads and <see cref="TripleBandLinearOp.Axpyb"/> (full, one-element and empty coefficients) on
/// the first and second derivative operators of four meshers, each read through its product with a seeded vector; a
/// splitting solve; and the refusals of vectors of another size and of a zero pivot.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TripleBandLinearOpVectorTests
{
    [Fact]
    public void TripleBandLinearOpVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/operators/triplebandlinearop");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            FdmMesherComposite mesher = MesherOf(c.Input("mesher"), c.Context());
            int d = c.Int("direction");
            var x = new FirstDerivativeOp(d, mesher);
            var y = new SecondDerivativeOp(d, mesher);
            Vector u = VectorOf(c.Input("u")), r = VectorOf(c.Input("r"));
            double[] a = Doubles(c.Input("a")), b = Doubles(c.Input("b"));
            check.Output(c, "mult", x.Mult(u).Apply(r).ToArray());
            check.Output(c, "multR", x.MultR(u).Apply(r).ToArray());
            check.Output(c, "addOp", y.Add(x).Apply(r).ToArray());
            check.Output(c, "addArray", x.Add(u).Apply(r).ToArray());
            var axpyb = new List<object?>();
            foreach (var lengths in c.Input("axpyb").EnumerateArray())
            {
                var z = new TripleBandLinearOp(d, mesher);
                z.Axpyb(new Vector(a[..lengths[0].GetInt32()]), x, y, new Vector(b[..lengths[1].GetInt32()]));
                axpyb.Add(z.Apply(r).ToArray());
            }

            check.Output(c, "axpyb", axpyb);
            TripleBandLinearOp m = y.Mult(u);
            check.Output(
                c,
                "solveSplitting",
                c.Input("splitting").EnumerateArray()
                    .Select(ab => (object?)m.SolveSplitting(r, ab[0].GetDouble(), ab[1].GetDouble()).ToArray()).ToArray());
            var shortR = new Vector(r.ToArray()[..^1]);
            check.Output(c, "applyShort", () => x.Apply(shortR).ToArray());
            check.Output(c, "multRShort", () => x.MultR(shortR).Apply(r).ToArray());
            check.Output(c, "solveShort", () => m.SolveSplitting(shortR, 0.5, 1.0).ToArray());
            check.Output(c, "solveZero", () => m.SolveSplitting(r, 0.0, 0.0).ToArray());
        }

        check.Complete();
    }
}
