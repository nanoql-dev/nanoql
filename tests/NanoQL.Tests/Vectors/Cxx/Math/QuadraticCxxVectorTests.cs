// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's quadratic vectors, <c>tests/vectors/cxx/math/quadratic.json</c>: <see cref="Quadratic"/>'s turning
/// point and its value, a value, the discriminant and the roots, with two roots, a double root and none.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class QuadraticCxxVectorTests
{
    [Fact]
    public void QuadraticCxxVector()
    {
        var file = VectorFile.Load("cxx/math/quadratic");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var q = new Quadratic(c.Double("a"), c.Double("b"), c.Double("c"));
            check.Output(c, "turningPoint", q.TurningPoint());
            check.Output(c, "valueAtTurningPoint", q.ValueAtTurningPoint());
            check.Output(c, "value", q.Evaluate(c.Double("x")));
            check.Output(c, "discriminant", q.Discriminant());
            bool found = q.Roots(out double x, out double y);
            check.Output(c, "rootsFound", found);
            check.Output(c, "x", x);
            check.Output(c, "y", y);
        }

        check.Complete();
    }
}
