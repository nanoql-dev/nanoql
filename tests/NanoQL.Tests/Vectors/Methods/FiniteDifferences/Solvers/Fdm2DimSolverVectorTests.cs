// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Fdm2DimSolver vectors, <c>tests/vectors/methods/finitedifferences/solvers/fdm2dimsolver.json</c>: swaptions
/// rolled back on the G2 operator under three schemes, the solver's interpolation and derivatives at points of the
/// factors' plane, and its theta at those of them where it doesn't cancel.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Fdm2DimSolverVectorTests
{
    [Fact]
    public void Fdm2DimSolverVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/solvers/fdm2dimsolver");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            (ShortRateModel model, FdmMesherComposite mesher, FdmSolverDesc desc) = ShortRateFdInputs.SetupOf(c.Input("setup"), c.Context());
            var solver = new Fdm2DimSolver(desc, SchemeOf(c.Input("scheme")), new FdmG2Op(mesher, (G2)model, 0, 1));
            (double X, double Y)[] points = [.. c.Input("points").EnumerateArray().Select(p => (p[0].GetDouble(), p[1].GetDouble()))];
            check.Output(c, "interpolateAt", points.Select(p => (object?)solver.InterpolateAt(p.X, p.Y)).ToArray());
            check.Output(
                c,
                "thetaAt",
                c.Input("thetaPoints").EnumerateArray().Select(p => (object?)solver.ThetaAt(p[0].GetDouble(), p[1].GetDouble())).ToArray());
            check.Output(c, "derivativeX", points.Select(p => (object?)solver.DerivativeX(p.X, p.Y)).ToArray());
            check.Output(c, "derivativeY", points.Select(p => (object?)solver.DerivativeY(p.X, p.Y)).ToArray());
            check.Output(c, "derivativeXX", points.Select(p => (object?)solver.DerivativeXX(p.X, p.Y)).ToArray());
            check.Output(c, "derivativeYY", points.Select(p => (object?)solver.DerivativeYY(p.X, p.Y)).ToArray());
            check.Output(c, "derivativeXY", points.Select(p => (object?)solver.DerivativeXY(p.X, p.Y)).ToArray());
        }

        check.Complete();
    }
}
