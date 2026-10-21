// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FirstDerivativeOp vectors, <c>tests/vectors/methods/finitedifferences/operators/firstderivativeop.json</c>: the
/// first derivative operator on four meshers in each direction, applied to seeded vectors and solved by splitting at
/// two (a, b).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FirstDerivativeOpVectorTests
{
    [Fact]
    public void FirstDerivativeOpVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/operators/firstderivativeop");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            CheckDerivativeOp(check, c, (direction, mesher) => new FirstDerivativeOp(direction, mesher));
        }

        check.Complete();
    }
}
