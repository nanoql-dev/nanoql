// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Boost's bracketing root finders' harness vectors, <c>tests/vectors/cxx/boost/math/tools/toms748_solve.json</c>:
/// <c>toms748_solve</c> and <c>bracket_and_solve_root</c> on fixed functions, each output the final bracket and the
/// calls made, read through nanoql's internal <c>Toms748SolveFunctions</c>, whose methods take their call limit by
/// reference and their tolerance as an internal struct, so they are invoked through reflection.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Toms748SolveCxxVectorTests
{
    // the harness's functions, as tools/cxx/src/boost/math/tools/toms748_solve.cpp writes them
    private static readonly Dictionary<string, Func<double, double>> Functions = new()
    {
        ["x x x - 2"] = x => x * x * x - 2.0,
        ["exp(x) - 3"] = x => Math.Exp(x) - 3.0,
        ["log(x) - 1"] = x => Math.Log(x) - 1.0,
        ["2 - x x"] = x => 2.0 - x * x,
        ["1/x - 3"] = x => 1.0 / x - 3.0,
        ["x - 1e-300"] = x => x - 1e-300,
        ["atan(x) - 1.5"] = x => Math.Atan(x) - 1.5,
        ["x + 0.5"] = x => x + 0.5,
        ["1 - exp(x)"] = x => 1.0 - Math.Exp(x),
    };

    private static readonly Type Solver =
        typeof(VanillaOption).Assembly.GetType("NanoQL.Numerics.Toms748SolveFunctions", throwOnError: true)!;

    private static readonly Type Tolerance = Solver.GetNestedType("EpsTolerance", BindingFlags.NonPublic)!;

    private static readonly MethodInfo Solve = Solver.GetMethod(
        "Toms748Solve",
        BindingFlags.NonPublic | BindingFlags.Static,
        [typeof(Func<double, double>), typeof(double), typeof(double), Tolerance, typeof(ulong).MakeByRefType()])!;

    private static readonly MethodInfo Bracket = Solver.GetMethod(
        "BracketAndSolveRoot",
        BindingFlags.NonPublic | BindingFlags.Static,
        [typeof(Func<double, double>), typeof(double), typeof(double), typeof(bool), Tolerance,
            typeof(ulong).MakeByRefType()])!;

    [Fact]
    public void Toms748SolveCxxVector()
    {
        var file = VectorFile.Load("cxx/boost/math/tools/toms748_solve");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.Name)
            {
                case "toms748_solve":
                    check.Output(c, "result", (object?[])[.. c.Input("solve").EnumerateArray().Select(e => Lazy(() => Invoke(
                        Solve,
                        Functions[e.GetProperty("f").GetString()!],
                        e.GetProperty("a").GetDouble(),
                        e.GetProperty("b").GetDouble(),
                        Tol(e),
                        MaxIter(e))))]);
                    break;

                case "bracket_and_solve_root":
                    check.Output(c, "result", (object?[])[.. c.Input("bracket").EnumerateArray().Select(e => Lazy(() => Invoke(
                        Bracket,
                        Functions[e.GetProperty("f").GetString()!],
                        e.GetProperty("guess").GetDouble(),
                        e.GetProperty("factor").GetDouble(),
                        e.GetProperty("rising").GetBoolean(),
                        Tol(e),
                        MaxIter(e))))]);
                    break;

                default:
                    throw new InvalidOperationException($"unknown case {c.Name}");
            }
        }

        check.Complete();
    }

    private static object? Lazy(Func<object?> f) => f;

    private static object Tol(JsonElement e) =>
        Activator.CreateInstance(
            Tolerance, BindingFlags.NonPublic | BindingFlags.Instance, null, [e.GetProperty("bits").GetInt32()], null)!;

    private static ulong MaxIter(JsonElement e) => (ulong)e.GetProperty("maxIter").GetInt64();

    // [the bracket's ends, the calls made]; the solver's own exception, not reflection's wrapper, where it throws
    private static object?[] Invoke(MethodInfo m, params object[] args)
    {
        object result;
        try
        {
            result = m.Invoke(null, args)!;
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            throw;
        }

        (double a, double b) = ((double, double))result;
        return [a, b, (double)(ulong)args[^1]];
    }
}
