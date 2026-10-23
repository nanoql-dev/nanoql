// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The short-rate finite-difference vectors' specs, <c>tools/vectors/gen/methods/finitedifferences/operators/fdmg2op.py</c>'s:
/// the G2 and Hull-White models, and the checks of their operators.
/// </summary>
internal static class ShortRateFdInputs
{
    /// <summary>The short-rate model a spec describes: a G2 or a Hull-White model on its curve.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The model.</returns>
    public static ShortRateModel ModelOf(JsonElement spec)
    {
        var curve = CashFlowInputs.CurveOf(spec.GetProperty("curve"));
        double Real(string name) => spec.GetProperty(name).GetDouble();
        return spec.GetProperty("type").GetString() switch
        {
            "G2" => new G2(curve, Real("a"), Real("sigma"), Real("b"), Real("eta"), Real("rho")),
            "HullWhite" => new HullWhite(curve, Real("a"), Real("sigma")),
            string s => throw new InvalidOperationException($"unknown model '{s}'"),
            null => throw new InvalidOperationException("no model type"),
        };
    }

    /// <summary>
    /// The finite-difference swaption engines' set-up a spec of
    /// <c>tools/vectors/gen/methods/finitedifferences/solvers/fdmg2solver.py</c> describes, built as the engines build
    /// it: each factor's Ornstein-Uhlenbeck mesher to the last exercise, the swap's inner value on the discounting and
    /// forwarding models, the vanilla step conditions, no boundary conditions.
    /// </summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The discounting model, the mesher and the solver description.</returns>
    public static (ShortRateModel Model, FdmMesherComposite Mesher, FdmSolverDesc Desc) SetupOf(
        JsonElement spec, EvaluationContext context)
    {
        ShortRateModel model = ModelOf(spec.GetProperty("model"));
        ShortRateModel fwd = ModelOf(spec.GetProperty("forwardingModel"));
        FixedVsFloatingSwap swap = SwapInputs.SwapOf(spec.GetProperty("swap"), context);
        Exercise exercise = OptionInputs.ExerciseOf(spec.GetProperty("exercise"));
        YieldTermStructure ts = ((ITermStructureConsistentModel)model).TermStructure;
        DayCounter dc = ts.DayCounter;
        Date referenceDate = ts.ReferenceDate;
        double maturity = dc.YearFraction(referenceDate, exercise.LastDate());
        int[] sizes = [.. spec.GetProperty("sizes").EnumerateArray().Select(x => x.GetInt32())];
        double invEps = spec.GetProperty("invEps").GetDouble();
        var t2d = new Dictionary<double, Date>();
        foreach (Date d in exercise.Dates)
        {
            t2d[dc.YearFraction(referenceDate, d)] = d;
        }

        FdmMesherComposite mesher;
        FdmInnerValueCalculator calculator;
        if (model is G2 g2)
        {
            mesher = new FdmMesherComposite(
                new FdmSimpleProcess1dMesher(sizes[0], new OrnsteinUhlenbeckProcess(g2.A(), g2.Sigma), maturity, 1, invEps),
                new FdmSimpleProcess1dMesher(sizes[1], new OrnsteinUhlenbeckProcess(g2.B(), g2.Eta), maturity, 1, invEps));
            calculator = new FdmAffineG2ModelSwapInnerValue(g2, (G2)fwd, swap, t2d, mesher, 0, context);
        }
        else
        {
            var hw = (HullWhite)model;
            mesher = new FdmMesherComposite(
                new FdmSimpleProcess1dMesher(sizes[0], new OrnsteinUhlenbeckProcess(hw.A(), hw.Sigma), maturity, 1, invEps));
            calculator = new FdmAffineHullWhiteModelSwapInnerValue(hw, (HullWhite)fwd, swap, t2d, mesher, 0, context);
        }

        FdmStepConditionComposite conditions = FdmStepConditionComposite.VanillaComposite(
            [], exercise, mesher, calculator, referenceDate, dc, context);
        var desc = new FdmSolverDesc(
            mesher, [], conditions, calculator, maturity, spec.GetProperty("tGrid").GetInt32(),
            spec.GetProperty("dampingSteps").GetInt32());
        return (model, mesher, desc);
    }

    /// <summary>Checks an operator's outputs at each of the case's times: its size, apply, apply_mixed where the case
    /// has it, each direction's apply and splitting solutions, and the preconditioner.</summary>
    /// <param name="check">The file's checker.</param>
    /// <param name="c">The case.</param>
    /// <param name="op">The operator.</param>
    /// <param name="dims">The mesher's number of directions.</param>
    public static void CheckOperator(VectorAssert check, VectorCase c, FdmLinearOpComposite op, int dims)
    {
        double[][] rs = [.. c.Input("r").EnumerateArray().Select(r => r.EnumerateArray().Select(x => x.GetDouble()).ToArray())];
        double[] splitting = [.. c.Input("splitting").EnumerateArray().Select(x => x.GetDouble())];
        double preconditioner = c.Input("preconditioner").GetDouble();
        int n = rs[0].Length;
        var apply = new List<object?>();
        var applyMixed = new List<object?>();
        var applyDirection = new List<object?>();
        var solveSplitting = new List<object?>();
        var precondition = new List<object?>();
        foreach (JsonElement times in c.Input("times").EnumerateArray())
        {
            op.SetTime(times[0].GetDouble(), times[1].GetDouble());
            apply.Add(rs.Select(r => (object?)Applied(n, r, (x, y) => op.Apply(x, y))).ToArray());
            applyMixed.Add(rs.Select(r => (object?)Applied(n, r, op.ApplyMixed)).ToArray());
            applyDirection.Add(Enumerable.Range(0, dims)
                .Select(d => (object?)Applied(n, rs[0], (x, y) => op.ApplyDirection(d, x, y))).ToArray());
            solveSplitting.Add(Enumerable.Range(0, dims).Select(d => (object?)splitting
                .Select(s => (object?)Applied(n, rs[0], (x, y) => op.SolveSplitting(d, x, s, y))).ToArray()).ToArray());
            precondition.Add(Applied(n, rs[0], (x, y) => op.Preconditioner(x, preconditioner, y)));
        }

        check.Output(c, "size", op.Size);
        check.Output(c, "apply", apply);
        if (c.Expected.TryGetProperty("applyMixed", out _))
        {
            check.Output(c, "applyMixed", applyMixed);
        }

        check.Output(c, "applyDirection", applyDirection);
        check.Output(c, "solveSplitting", solveSplitting);
        check.Output(c, "preconditioner", precondition);
    }

    private static double[] Applied(int n, double[] r, SpanAction f)
    {
        double[] result = new double[n];
        f(r, result);
        return result;
    }

    private delegate void SpanAction(ReadOnlySpan<double> r, Span<double> result);
}
