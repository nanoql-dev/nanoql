// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Heston model vectors, <c>tests/vectors/cxx/models/equity/hestonmodel.json</c>:
/// <see cref="HestonModelFellerConstraint"/>'s <see cref="Constraint.Test"/>, <see cref="Constraint.UpperBound"/>,
/// <see cref="Constraint.LowerBound"/> and <see cref="Constraint.Update"/> at twenty parameter vectors, read as
/// <see cref="ConstraintCxxVectorTests"/> reads the constraints'.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class HestonModelCxxVectorTests
{
    [Fact]
    public void HestonModelCxxVector()
    {
        var file = VectorFile.Load("cxx/models/equity/hestonmodel");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            ConstraintCxxVectorTests.Check(check, c);
        }

        check.Complete();
    }
}
