// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmIndicesOnBoundary vectors,
/// <c>tests/vectors/methods/finitedifferences/utilities/fdmindicesonboundary.json</c>:
/// <see cref="FdmIndicesOnBoundary"/>'s indices for layouts of one to four dimensions, each direction on its lower and
/// upper side and on no side.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmIndicesOnBoundaryVectorTests
{
    [Fact]
    public void FdmIndicesOnBoundaryVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/utilities/fdmindicesonboundary");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var layout = new FdmLinearOpLayout(c.Input("dim").EnumerateArray().Select(d => d.GetInt32()));
            var indices = new FdmIndicesOnBoundary(
                layout, c.Input("direction").GetInt32(), (BoundaryConditionSide)c.Input("side").GetInt32());
            check.Output(c, "indices", indices.Indices.ToArray());
        }

        check.Complete();
    }
}
