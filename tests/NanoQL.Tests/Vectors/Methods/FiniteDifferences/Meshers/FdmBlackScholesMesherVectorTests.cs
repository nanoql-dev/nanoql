// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmBlackScholesMesher vectors,
/// <c>tests/vectors/methods/finitedifferences/meshers/fdmblackscholesmesher.json</c>:
/// <see cref="FdmBlackScholesMesher"/>'s locations, dplus and dminus over five processes, three maturities and three
/// strikes, then with edge constraints, tails, critical points, dividends, quanto helpers, spot adjustments and small
/// sizes; and the refusal of a null spot.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmBlackScholesMesherVectorTests
{
    [Fact]
    public void FdmBlackScholesMesherVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/meshers/fdmblackscholesmesher");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            CheckMesher1d(check, c);
        }

        check.Complete();
    }
}
