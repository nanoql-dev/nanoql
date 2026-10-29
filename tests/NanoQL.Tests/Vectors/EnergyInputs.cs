// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Builds the energy processes the harness's experimental vector files describe, as
/// <c>tools/cxx/src/experimental/processes/extouspec.hpp</c> builds them.
/// </summary>
/// <remarks>
/// An extended OU process is <c>{"speed", "sigma", "x0", "level", "discretization", "intEps"}</c>, its level
/// <c>{"type": "constant", "b"}</c> or <c>{"type": "linear", "c0", "c1"}</c>; a jump process <c>{"ou", "y0", "beta",
/// "jumpIntensity", "eta"}</c>; a Kluge-OU process <c>{"kluge", "gas", "rho"}</c>.
/// </remarks>
internal static class EnergyInputs
{
    /// <summary>The extended Ornstein-Uhlenbeck process a spec describes.</summary>
    /// <param name="s">The spec.</param>
    /// <returns>The process.</returns>
    public static ExtendedOrnsteinUhlenbeckProcess ExtendedOU(JsonElement s) =>
        new(
            s.GetProperty("speed").GetDouble(),
            s.GetProperty("sigma").GetDouble(),
            s.GetProperty("x0").GetDouble(),
            Level(s.GetProperty("level")),
            (ExtendedOrnsteinUhlenbeckProcessDiscretization)s.GetProperty("discretization").GetInt32(),
            s.GetProperty("intEps").GetDouble());

    /// <summary>The jump process a spec describes.</summary>
    /// <param name="s">The spec.</param>
    /// <returns>The process.</returns>
    public static ExtOUWithJumpsProcess Jumps(JsonElement s) =>
        new(
            ExtendedOU(s.GetProperty("ou")),
            s.GetProperty("y0").GetDouble(),
            s.GetProperty("beta").GetDouble(),
            s.GetProperty("jumpIntensity").GetDouble(),
            s.GetProperty("eta").GetDouble());

    /// <summary>The Kluge-OU process a spec describes.</summary>
    /// <param name="s">The spec.</param>
    /// <returns>The process.</returns>
    public static KlugeExtOUProcess KlugeOU(JsonElement s) =>
        new(s.GetProperty("rho").GetDouble(), Jumps(s.GetProperty("kluge")), ExtendedOU(s.GetProperty("gas")));

    /// <summary>A seasonal shape, a list of [time, value], or <see langword="null"/> for none.</summary>
    /// <param name="s">The shape.</param>
    /// <returns>The pairs.</returns>
    public static (double Time, double Value)[]? Shape(JsonElement s) =>
        s.ValueKind == JsonValueKind.Null ? null : [.. s.EnumerateArray().Select(p => (p[0].GetDouble(), p[1].GetDouble()))];

    private static Func<double, double> Level(JsonElement s)
    {
        if (s.GetProperty("type").GetString() == "constant")
        {
            double b = s.GetProperty("b").GetDouble();
            return _ => b;
        }

        double c0 = s.GetProperty("c0").GetDouble(), c1 = s.GetProperty("c1").GetDouble();
        return t => c0 + (c1 * t);
    }
}
