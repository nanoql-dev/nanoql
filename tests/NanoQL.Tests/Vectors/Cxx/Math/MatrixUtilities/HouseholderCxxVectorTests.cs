// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.BasketInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Householder vectors, <c>tests/vectors/cxx/math/matrixutilities/householder.json</c>:
/// <see cref="HouseholderTransformation"/>'s value and matrix in one to six dimensions, and
/// <see cref="HouseholderReflection"/>'s value and reflection vector onto axes and seeded unit vectors, near and on e,
/// and of the zero vector (refused).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class HouseholderCxxVectorTests
{
    [Fact]
    public void HouseholderCxxVector()
    {
        var file = VectorFile.Load("cxx/math/matrixutilities/householder");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Name.StartsWith("transformation", StringComparison.Ordinal))
            {
                var v = new Vector(c.Doubles("v"));
                var x = new Vector(c.Doubles("x"));
                check.Output(c, "value", new HouseholderTransformation(v).Evaluate(x).ToArray());
                if (c.Name != "transformation by the zero vector")
                {
                    check.Output(c, "matrix", Flat(new HouseholderTransformation(v).GetMatrix()));
                }
            }
            else
            {
                var e = new Vector(c.Doubles("e"));
                var a = new Vector(c.Doubles("a"));
                check.Output(c, "value", () => new HouseholderReflection(e).Evaluate(a).ToArray());
                check.Output(c, "reflectionVector", () => new HouseholderReflection(e).ReflectionVector(a).ToArray());
            }
        }

        check.Complete();
    }
}
