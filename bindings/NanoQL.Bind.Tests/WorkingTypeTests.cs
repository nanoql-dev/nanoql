// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Text.Json;

namespace NanoQL.Bind.Tests;

/// <summary>
/// The working types at the boundary (docs/bindings.md, "Working types"): no marked type behind any handle in the
/// checked-in surface; <c>Matrix</c> and <c>Vector</c> as arrays; generators as snapshots drawn in batches, a user's too;
/// the stepping parts withheld.
/// </summary>
public class WorkingTypeTests
{
    private static readonly Assembly[] Nanoql = [typeof(Time.Date).Assembly, typeof(Extensions.Conventions.ConventionSet).Assembly];

    [Fact]
    public void NoMarkedTypeIsBehindAnyHandleInTheCheckedInSurface()
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Surfaces.Repository, "bindings", "surface.json")));
        var marked = new List<string>();
        // Every object a kind carries: the type itself (a nullable's underlying one), a collection's elements and a
        // map's values.
        void Check(string where, JsonElement kind)
        {
            string k = kind.GetProperty("kind").GetString()!;
            var types = new List<string?>();
            if (k is "handle" or "boxed" or "boxed?" or "snapshot")
            {
                string type = kind.GetProperty("type").GetString()!;
                types.Add(type.StartsWith("System.Nullable`1<", StringComparison.Ordinal) ? type["System.Nullable`1<".Length..^1] : type);
            }

            if (k.StartsWith("array:", StringComparison.Ordinal) && kind.TryGetProperty("element", out JsonElement e))
            {
                types.Add(e.GetString());
            }

            if (k.StartsWith("map:", StringComparison.Ordinal) && kind.TryGetProperty("value", out JsonElement v))
            {
                types.Add(v.GetString());
            }

            foreach (string? type in types)
            {
                if (type != null && Find(type) is { } t && IsWorking(t) && k != "snapshot")
                {
                    marked.Add($"{where}: {type}");
                }
            }
        }

        int checkedKinds = 0;
        foreach (JsonElement f in doc.RootElement.GetProperty("functions").EnumerateArray())
        {
            string name = f.GetProperty("name").GetString()!;
            foreach (JsonElement p in f.GetProperty("params").EnumerateArray())
            {
                Check(name, p);
                checkedKinds++;
            }

            if (f.GetProperty("returns").ValueKind == JsonValueKind.Object)
            {
                Check(name, f.GetProperty("returns"));
            }
        }

        foreach (JsonElement t in doc.RootElement.GetProperty("types").EnumerateArray())
        {
            if (t.GetProperty("form").GetString() == "handle" && Find(t.GetProperty("name").GetString()!) is { } type && IsWorking(type))
            {
                marked.Add("type " + type.FullName);
            }
        }

        Assert.True(checkedKinds > 10000, $"the surface's parameters were read: {checkedKinds}");
        Assert.Empty(marked);
    }

    [Fact]
    public void AMarkedStructIsKeptOffHandlesTakenDirectlyOrNullable()
    {
        Generation g = Surfaces.WithFixtures;
        Assert.Null(Surfaces.Declaration(g, "fx_MarkedHolder_TakeDirect"));
        Assert.Null(Surfaces.Declaration(g, "fx_MarkedHolder_TakeNullable"));
        Assert.Null(Surfaces.Declaration(g, "fx_MarkedHolder_GiveNullable"));
        Assert.Contains(g.Surface.Skips, s => s.Display.StartsWith("MarkedHolder.TakeNullable", StringComparison.Ordinal) && s.Reason.Contains("MarkedValue?", StringComparison.Ordinal));
    }

    [Fact]
    public void TheMatrixAndVectorArgumentsAMemberWritesAreFoundFromIl()
    {
        // The stable functions that write into an argument (the member, an override, or the documentation of an
        // abstract member saying so), and none of the readers the scan once took for writers.
        string[] stable = [.. Surfaces.Nanoql.Surface.Written.Where(w => !w.Function.Experimental).Select(w => w.Function.CName + ":" + w.Parameter.Name).Order(StringComparer.Ordinal)];
        Assert.Equal(
            [
                "nql_Constraint_Update:p", "nql_CostFunction_Gradient:grad", "nql_CostFunction_Jacobian:jac", "nql_CostFunction_ValueAndGradient:grad",
                "nql_CostFunction_ValuesAndJacobian:jac", "nql_LeastSquareFunction_Gradient:grad", "nql_LeastSquareFunction_ValueAndGradient:grad",
                "nql_LeastSquareProblem_TargetAndValue:fct2fit", "nql_LeastSquareProblem_TargetAndValue:target",
                "nql_LeastSquareProblem_TargetValueAndGradient:fct2fit", "nql_LeastSquareProblem_TargetValueAndGradient:gradFct2fit",
                "nql_LeastSquareProblem_TargetValueAndGradient:target",
            ],
            stable);
        Assert.Contains(Surfaces.Nanoql.Surface.Written, w => w.Function.CName == "nql_TemperatureBoltzmann_Update" && w.Parameter.Name == "newTemp");
        Assert.DoesNotContain(Surfaces.Nanoql.Surface.Written, w => w.Function.CName is "nql_HestonProcess_Diffusion" or "nql_ArrayFunctions_Norm2" or "nql_ReannealingTrivial_Reanneal");
    }

    [Fact]
    public void AWriteThroughASpanIsFound()
    {
        string[] written = [.. Surfaces.WithFixtures.Surface.Written.Where(w => w.Function.Prefix == "fx").Select(w => w.Function.CName).Order(StringComparer.Ordinal)];
        Assert.Equal(["fx_SpanWriters_CopyInto", "fx_SpanWriters_Fill", "fx_SpanWriters_Row", "fx_SpanWriters_ThroughHelper", "fx_SpanWriters_ThroughLocal"], written.Where(n => n.StartsWith("fx_SpanWriters_", StringComparison.Ordinal)));
        Assert.DoesNotContain("fx_SpanWriters_Reader", written);
    }

    [Fact]
    public void AUsersOverrideDoesntChangeTheFormOfNanoqlsFunctions()
    {
        Generation g = Surfaces.WithFixtures;
        Assert.DoesNotContain(g.Surface.Written, w => w.Function.CName == "nql_CostFunction_Values");
        Assert.Equal(
            Surfaces.Nanoql.Surface.Written.Select(w => w.Function.CName + ":" + w.Parameter.Name).Order(StringComparer.Ordinal),
            g.Surface.Written.Where(w => w.Function.Prefix == "nql").Select(w => w.Function.CName + ":" + w.Parameter.Name).Order(StringComparer.Ordinal));
        Assert.Contains(g.Surface.WrittenElsewhere, w => w.Function.CName == "nql_CostFunction_Values" && w.Parameter.Name == "x" && w.Prefix == "fx");
    }

    [Fact]
    public void MatrixAndVectorCrossAsArraysAndTheirOwnMembersDont()
    {
        Generation g = Surfaces.Nanoql;
        Assert.DoesNotContain(g.Surface.Functions, f => f.Type.FullName is "NanoQL.Numerics.Matrix" or "NanoQL.Numerics.Vector");
        Assert.Contains("const double* matrix, int32_t matrix_rows, int32_t matrix_columns", Surfaces.Declaration(g, "nql_PseudoSqrtFunctions_PseudoSqrt"), StringComparison.Ordinal);
    }

    [Fact]
    public void AGeneratorIsASnapshotDrawnInBatches()
    {
        Assert.Equal("NQL_API nql_status nql_MersenneTwisterUniformRng_Draws(nql_handle self, int32_t count, double** values, double** weights, nql_handle* next);", Surfaces.Declaration(Surfaces.Nanoql, "nql_MersenneTwisterUniformRng_Draws"));
        Assert.Equal("NQL_API nql_status nql_SobolRsg_Draws(nql_handle self, int32_t count, double** values, double** weights, int32_t* dimension, nql_handle* next);", Surfaces.Declaration(Surfaces.Nanoql, "nql_SobolRsg_Draws"));
        Assert.Null(Surfaces.Declaration(Surfaces.Nanoql, "nql_MersenneTwisterUniformRng_Next"));
    }

    [Fact]
    public void AUsersGeneratorIsKeptOffHandlesThroughTheInterfacesMark()
    {
        Generation g = Surfaces.WithFixtures;
        Assert.Equal("snapshot", g.Surface.Types.Single(t => t.Type.FullName == "NanoQL.Bind.Tests.Fixtures.UserRng").Form);
        Assert.NotNull(Surfaces.Declaration(g, "fx_UserRng_Draws"));
        Assert.Null(Surfaces.Declaration(g, "fx_UserRng_Next"));
    }

    [Fact]
    public void TheSteppingPartsAreWithheldWithTheirReason()
    {
        Generation g = Surfaces.Nanoql;
        Assert.Equal("withheld", g.Surface.Types.Single(t => t.Type.FullName == "NanoQL.Methods.TridiagonalOperator").Form);
        Assert.Contains(g.Surface.Skips, s => s.Display.StartsWith("TridiagonalOperator.", StringComparison.Ordinal) && s.Reason.Contains("working type", StringComparison.Ordinal));
    }

    private static Type? Find(string name) => Nanoql.Select(a => a.GetType(name)).FirstOrDefault(t => t != null);

    private static bool IsWorking(Type t) =>
        t.GetCustomAttribute<WorkingTypeAttribute>(inherit: true) != null || t.GetInterfaces().Any(i => i.GetCustomAttribute<WorkingTypeAttribute>() != null);
}
