// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmG2Solver vectors, <c>tests/vectors/methods/finitedifferences/solvers/fdmg2solver.json</c>: European and
/// Bermudan swaptions rolled back on the G2 operator under seven schemes, read at points of the factors' plane.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmG2SolverVectorTests
{
    [Fact]
    public void FdmG2SolverVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/solvers/fdmg2solver");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            (ShortRateModel model, _, FdmSolverDesc desc) = ShortRateFdInputs.SetupOf(c.Input("setup"), c.Context());
            var solver = new FdmG2Solver((G2)model, desc, SchemeOf(c.Input("scheme")));
            check.Output(
                c,
                "valueAt",
                c.Input("points").EnumerateArray().Select(p => (object?)solver.ValueAt(p[0].GetDouble(), p[1].GetDouble())).ToArray());
        }

        check.Complete();
    }
}
