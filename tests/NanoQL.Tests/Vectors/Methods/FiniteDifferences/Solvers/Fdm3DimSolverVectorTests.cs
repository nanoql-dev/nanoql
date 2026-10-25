// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Fdm3DimSolver vectors, <c>tests/vectors/methods/finitedifferences/solvers/fdm3dimsolver.json</c>: the
/// Heston-Hull-White solver's set-ups rolled back on the operator directly, read at points in the mesher's coordinates:
/// the interpolated value, and the theta where it doesn't cancel.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Fdm3DimSolverVectorTests
{
    [Fact]
    public void Fdm3DimSolverVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/solvers/fdm3dimsolver");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement setup = c.Input("setup");
            (HestonProcess process, HullWhiteProcess hullWhite, FdmSolverDesc desc) =
                FdmHestonHullWhiteSolverVectorTests.SetupOf(setup, context);
            var op = new FdmHestonHullWhiteOp(desc.Mesher, process, hullWhite, setup.GetProperty("correlation").GetDouble());
            var solver = new Fdm3DimSolver(desc, SchemeOf(c.Input("scheme")), op);
            (double X, double Y, double Z)[] points = FdmHestonHullWhiteSolverVectorTests.Points(c.Input("points"));
            (double X, double Y, double Z)[] thetaPoints = FdmHestonHullWhiteSolverVectorTests.Points(c.Input("thetaPoints"));
            check.Output(c, "interpolateAt", points.Select(p => Lazy(() => solver.InterpolateAt(p.X, p.Y, p.Z))).ToArray());
            check.Output(c, "thetaAt", thetaPoints.Select(p => Lazy(() => solver.ThetaAt(p.X, p.Y, p.Z))).ToArray());
        }

        check.Complete();
    }
}
