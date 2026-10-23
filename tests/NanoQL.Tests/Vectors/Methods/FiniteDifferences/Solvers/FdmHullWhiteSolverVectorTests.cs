// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmHullWhiteSolver vectors, <c>tests/vectors/methods/finitedifferences/solvers/fdmhullwhitesolver.json</c>:
/// European and Bermudan swaptions rolled back on the Hull-White operator under seven schemes, read at points of the
/// factor's line.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmHullWhiteSolverVectorTests
{
    [Fact]
    public void FdmHullWhiteSolverVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/solvers/fdmhullwhitesolver");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            (ShortRateModel model, _, FdmSolverDesc desc) = ShortRateFdInputs.SetupOf(c.Input("setup"), c.Context());
            var solver = new FdmHullWhiteSolver((HullWhite)model, desc, SchemeOf(c.Input("scheme")));
            check.Output(c, "valueAt", c.Input("points").EnumerateArray().Select(p => (object?)solver.ValueAt(p.GetDouble())).ToArray());
        }

        check.Complete();
    }
}
