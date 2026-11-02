// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Text.Json;
using NanoQL.Bind.Model;
using NanoQL.Bind.Selecting;

namespace NanoQL.Bind.Tests;

/// <summary>
/// The C ABI's forms and rules after S-bind2's contract (docs/bindings.md): tuples in, as a parameter per element or an
/// array per element and a count, and collections of tuples out; a bound engine's nullable inputs open on both sides
/// as null; optional parameters of a type that doesn't cross left at their defaults; a closed generator construction
/// taking a deriving class's snapshot; a type's own members hiding its base's; statics under their declaring type; and
/// Python's classes each with a constructor of their own.
/// </summary>
public class ContractTests
{
    private static string? Fx(string name) => Surfaces.Declaration(Surfaces.WithFixtures, name);

    private static string? Nql(string name) => Surfaces.Declaration(Surfaces.Nanoql, name);

    private static CFunction Function(Generation g, string name) => g.Surface.Functions.Single(f => f.CName == name);

    [Fact]
    public void ATupleIsAParameterPerElementInItsOwnForm()
    {
        Assert.Equal("NQL_API nql_status fx_TupleKinds_Single(const double* cPoint_point, const double* cPoint_density, double* result);", Fx("fx_TupleKinds_Single"));
        Assert.Equal("NQL_API nql_status fx_TupleKinds_Scalars(int32_t x_count, int64_t x_total, bool x_flag, int32_t* result);", Fx("fx_TupleKinds_Scalars"));
        Assert.Equal("NQL_API nql_status fx_TupleKinds_Values(const char* x_name, nql_date x_when, nql_period x_tenor, int32_t* result);", Fx("fx_TupleKinds_Values"));
        Assert.Equal("NQL_API nql_status fx_TupleKinds_Others(int32_t x_unit, nql_complex x_z, int32_t* result);", Fx("fx_TupleKinds_Others"));
    }

    [Fact]
    public void ACollectionOfTuplesIsAnArrayPerElementAndACount()
    {
        Assert.Equal(
            "NQL_API nql_status fx_TupleKinds_Rows(const double* rows_second, const bool* rows_second_has_value, const int32_t* rows_third, const bool* rows_third_has_value, const nql_date* rows_when, int32_t rows_count, int32_t* result);",
            Fx("fx_TupleKinds_Rows"));
        Assert.Equal("NQL_API nql_status fx_TupleKinds_Objects(const nql_handle* rows_item1, const double* rows_item2, int32_t rows_count, int32_t* result);", Fx("fx_TupleKinds_Objects"));
        Assert.Equal(
            "NQL_API nql_status nql_AndreasenHugeVolatilityInterpl_new(const nql_handle* calibrationSet_option, const double* calibrationSet_volatility, int32_t calibrationSet_count, double spot, nql_handle rTS, nql_handle qTS, int32_t interpolationType, int32_t calibrationType, int32_t nGridPoints, const double* minStrike, const double* maxStrike, nql_handle optimizationMethod, nql_handle endCriteria, nql_handle* result);",
            Nql("nql_AndreasenHugeVolatilityInterpl_new"));
    }

    [Fact]
    public void ACollectionOfTuplesOutIsAnArrayPerElementTheFlagsAndTheCount()
    {
        Assert.Equal("NQL_API nql_status fx_TupleKinds_Pairs(int32_t** first, char*** second, int32_t* count);", Fx("fx_TupleKinds_Pairs"));
        Assert.Equal("NQL_API nql_status fx_TupleKinds_Gaps(int32_t** left, int32_t** right, bool** has_value, int32_t* count);", Fx("fx_TupleKinds_Gaps"));
        Assert.Equal("NQL_API nql_status fx_TupleKinds_Measures(double** x, bool** flag, nql_date** when, int32_t* count);", Fx("fx_TupleKinds_Measures"));
        Assert.Equal("NQL_API nql_status fx_TupleKinds_Things(int32_t** unit, nql_complex** z, nql_handle** item, int32_t* count);", Fx("fx_TupleKinds_Things"));
        Assert.Equal(
            "NQL_API nql_status nql_EvolutionDescription_get_RelevanceRates(nql_handle self, int32_t** first, int32_t** second, int32_t* count);",
            Nql("nql_EvolutionDescription_get_RelevanceRates"));
    }

    [Fact]
    public void AnElementNamedLikeACollectionsCountTakesAnUnderscore()
    {
        Assert.Equal(
            "NQL_API nql_status fx_TupleKinds_Counted(const nql_period* rows_tenor, const int64_t* rows_count_, int32_t rows_count, nql_period** tenor, int64_t** count_, int32_t* count);",
            Fx("fx_TupleKinds_Counted"));
    }

    [Fact]
    public void ATuplesOverloadIsNamedByValueTupleAndItsElements()
    {
        Assert.NotNull(Nql("nql_Concentrating1dMesher_new__double_double_int_ValueTupleOptdoubleOptdouble"));
        Assert.NotNull(Nql("nql_Concentrating1dMesher_new__double_double_int_ValueTupledoubledoubleboolArray"));
    }

    [Fact]
    public void ANestedTupleANullableTupleInAndANullableElementOutAreWithheld()
    {
        Generation g = Surfaces.WithFixtures;
        string Reason(string member) => g.Surface.Skips.Single(s => s.Key.StartsWith("NanoQL.Bind.Tests.Fixtures.TupleKinds::" + member + "(", StringComparison.Ordinal)).Reason;
        Assert.Equal("return: returned tuple element: nullable", Reason("NullableOut"));
        Assert.StartsWith("collection of ", Reason("NullableIn"), StringComparison.Ordinal);
        Assert.StartsWith("tuple element: ", Reason("Nested"), StringComparison.Ordinal);
    }

    [Fact]
    public void SurfaceJsonNamesATuplesElements()
    {
        using JsonDocument doc = JsonDocument.Parse(Surfaces.Nanoql.Files["surface.json"]);
        JsonElement F(string name) => doc.RootElement.GetProperty("functions").EnumerateArray().Single(f => f.GetProperty("name").GetString() == name);
        JsonElement set = F("nql_AndreasenHugeVolatilityInterpl_new").GetProperty("params")[0];
        Assert.Equal("array:tuple:handle,double", set.GetProperty("kind").GetString());
        Assert.Equal(["Option", "Volatility"], set.GetProperty("items").EnumerateArray().Select(i => i.GetString()));
        JsonElement single = F("nql_Concentrating1dMesher_new__double_double_int_ValueTupleOptdoubleOptdouble").GetProperty("params")[3];
        Assert.Equal("tuple:double?,double?", single.GetProperty("kind").GetString());
        JsonElement result = F("nql_MarkovFunctional_ArbitrageIndices").GetProperty("returns");
        Assert.Equal("array:tuple?:int32,int32", result.GetProperty("kind").GetString());
        Assert.Equal(["Left", "Right"], result.GetProperty("items").EnumerateArray().Select(i => i.GetString()));
    }

    [Fact]
    public void ABindLeavesACollectionOfTuplesOpenByItsFirstArrayAndANullableInputNull()
    {
        string dir = Path.Combine(Path.GetTempPath(), "nanoql-bind-shims-" + Guid.NewGuid().ToString("N"));
        try
        {
            Surfaces.Nanoql.WriteShims(dir);
            string shims = string.Concat(Directory.GetFiles(dir, "*.g.cs").Order(StringComparer.Ordinal).Select(File.ReadAllText));
            Assert.Contains("if (@shape_time != null) d_[\"shape\"] = ", shims, StringComparison.Ordinal);

            // ContinuousArithmeticAsianLevyEngine's currentAverage: a nullable value with no default is null when open,
            // where its non-nullable twin's is refused.
            Assert.Contains("b.TakeOr<double?>(\"currentAverage\", null);", shims, StringComparison.Ordinal);
            Assert.Contains("b.Take<double>(\"currentAverage\");", shims, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void AnOptionalParameterOfATypeThatDoesntCrossIsLeftAtItsNullDefault()
    {
        Assert.Equal("NQL_API nql_status fx_Optionals_Scaled(double x, double* result);", Fx("fx_Optionals_Scaled"));
        Assert.Equal([1, 2], Function(Surfaces.WithFixtures, "fx_Optionals_Scaled").Dropped);
        Assert.Null(Fx("fx_Optionals_Spanned"));
        Assert.Null(Fx("fx_Optionals_Required"));
        Assert.Equal("NQL_API nql_status nql_BFGS_new(nql_handle* result);", Nql("nql_BFGS_new"));
        Assert.Equal("NQL_API nql_status nql_Date_TodaysDate(nql_date* result);", Nql("nql_Date_TodaysDate"));
        Assert.NotNull(Nql("nql_GlobalBootstrap_new__void"));
    }

    [Fact]
    public void AClosedGeneratorConstructionTakesADerivingClasssSnapshot()
    {
        CFunction f = Function(Surfaces.Nanoql, "nql_InvCumulativeMersenneTwisterGaussianRsg_new__RandomSequenceGeneratorMersenneTwisterUniformRng");
        CParam p = Assert.Single(f.Params);
        Assert.Equal("snapshot", p.Kind.Name);
        Assert.True(p.Kind.Type.IsConstructedGenericType);
        Assert.Equal(
            "NQL_API nql_status nql_InvCumulativeMersenneTwisterGaussianRsg_new__RandomSequenceGeneratorMersenneTwisterUniformRng(nql_handle uniformSequenceGenerator, nql_handle* result);",
            Nql(f.CName));
    }

    [Fact]
    public void EveryClosedGeneratorConstructionParameterHasACrossingClassDerivingIt()
    {
        // Over every function by reflection: a snapshot parameter of a closed construction can take a crossing
        // generator's snapshot, one with a constructor of its own (round 1's B1-1).
        // The generator's metadata context is gone after its run: its types are read as the test process's own.
        static Type Loaded(Type t) => Type.GetType(t.AssemblyQualifiedName!, throwOnError: true)!;
        Generation g = Surfaces.Nanoql;
        var makers = g.Surface.Functions.Where(f => f.Kind == "new").Select(f => f.Type).ToHashSet();
        var snapshots = g.Surface.Types.Where(t => t.Form == "snapshot" && makers.Contains(t.Type)).Select(t => Loaded(t.Type)).ToList();
        var orphans = g.Surface.Functions.SelectMany(f => f.Params.Where(p => !p.IsSelf && p.Kind.Name == "snapshot" && p.Kind.Type.IsConstructedGenericType)
                .Where(p => !snapshots.Any(s => Loaded(p.Kind.Type).IsAssignableFrom(s))).Select(p => f.CName + "(" + p.Name + ")"))
            .ToList();
        Assert.Empty(orphans);
        Assert.Equal(16, g.Surface.Functions.Count(f => f.Params.Any(p => !p.IsSelf && p.Kind.Name == "snapshot" && p.Kind.Type.IsConstructedGenericType)));

        // RandomSequenceGenerator<IInt32Rng>, NextInt32Sequence's closing at its constraint, which no generator derives.
        Assert.Equal(
            "takes a closed generator construction no generator class derives: RandomSequenceGenerator<IInt32Rng>",
            g.Surface.Skips.Single(s => s.Key.StartsWith("NanoQL.Numerics.RandomSequenceGenerator::NextInt32Sequence(", StringComparison.Ordinal)).Reason);
    }

    [Fact]
    public void AMemberATypeDeclaresWithNewHidesItsBasesAnOverrideDoesnt()
    {
        Generation g = Surfaces.WithFixtures;
        CFunction create = Function(g, "fx_HiddenNamed_Create");
        Assert.Equal("NanoQL.Bind.Tests.Fixtures.HiddenNamed", create.Holder.FullName);
        Assert.Equal("NanoQL.Bind.Tests.Fixtures.HiddenNamed", create.Return!.Type.FullName);
        Assert.Equal("NanoQL.Bind.Tests.Fixtures.HiddenOverride", Function(g, "fx_HiddenOverride_Half").Holder.FullName);
        Assert.True(Function(g, "fx_HiddenOverride_Twice").Holder.IsConstructedGenericType);

        // nanoql's seven: each named class's own Create returns the class, the three year-on-year ones' too.
        foreach (string name in new[]
                 {
                     "PiecewiseFlatHazardRate", "ZabrLocalVolatilityInterpolatedSmileSection", "ZabrShortMaturityLognormalInterpolatedSmileSection",
                     "ZabrShortMaturityNormalInterpolatedSmileSection", "YoYInflationCapFloorTermPriceSurface",
                     "InterpolatedYoYInflationOptionletVolatilityCurve", "KInterpolatedYoYInflationOptionletVolatilitySurface",
                 })
        {
            CFunction f = Function(Surfaces.Nanoql, $"nql_{name}_Create");
            Assert.Equal(f.Type, f.Return!.Type);
        }
    }

    [Fact]
    public void AStaticStaysUnderItsDeclaringTypeButANamedClassCarriesItsClosings()
    {
        Generation g = Surfaces.WithFixtures;
        bool Has(string name) => g.Surface.Functions.Any(f => f.CName == name);
        Assert.True(Has("fx_HiddenNamed_get_Limit"));
        Assert.False(Has("fx_HiddenOverride_get_Limit"));
        Assert.False(Has("fx_HiddenOverride_Create"));
        Assert.False(Has("fx_HiddenGrand_Create"));

        // The closing that declares the factory crosses with it, as a type of its own.
        CFunction own = g.Surface.Functions.Single(f => f.BaseName == "Create" && f.Type.IsConstructedGenericType
            && f.Type.GetGenericTypeDefinition().Name == "Hidable`1" && f.Type.GetGenericArguments()[0].Name == "Cubic");
        Assert.Equal(own.Type, own.Return!.Type);
    }

    [Fact]
    public void NoStaticSitsOnAnotherTypeThanItsDeclaringOneButANamedClassesClosings()
    {
        // Over every function by reflection: a static's holder is its type, or the closing a named class names.
        var wrong = Surfaces.Nanoql.Surface.Functions
            .Where(f => f.Static && f.Member is MethodInfo or FieldInfo or PropertyInfo && f.Kind is "static" or "get" or "field" or "set" && f.Holder != f.Type)
            .Where(f => !(Selector.IsNamedClass(f.Type) && f.Type.BaseType == f.Holder))
            .Select(f => f.CName).ToList();
        Assert.Empty(wrong);
        // the five named classes' closings' Create: GlobalPiecewiseSplineCubicDiscount's, PiecewiseSplineCubicDiscount's
        // and the three interpolated smile sections'
        int kept = Surfaces.Nanoql.Surface.Functions.Count(f => f.Static && f.Holder != f.Type && f.Kind is "static" or "get" or "field");
        Assert.Equal(5, kept);

        // a deprecated named class's closing's Create is deprecated with it, by the class's message
        CFunction copied = Assert.Single(
            Surfaces.Nanoql.Surface.Functions, f => f.Static && f.Holder != f.Type && f.Type.Name == "PiecewiseSplineCubicDiscount");
        Assert.StartsWith("QuantLib-Python deprecates PiecewiseSplineCubicDiscount: ", copied.Deprecated, StringComparison.Ordinal);
        Assert.All(
            Surfaces.Nanoql.Surface.Functions.Where(f => f.Static && f.Holder != f.Type && f.Type.Name != "PiecewiseSplineCubicDiscount"),
            f => Assert.Null(f.Deprecated));
        Assert.DoesNotContain(Surfaces.Nanoql.Surface.Functions, f => f.CName is "nql_GaussianDefProbLM_FromCorrelation" or "nql_Brent_get_MaxFunctionEvaluations");
        Assert.Contains(Surfaces.Nanoql.Surface.Functions, f => f.CName == "nql_LatentModel_GaussianCopulaPolicy_GaussianCopulaPolicyInitTraits_FromCorrelation");
    }

    [Fact]
    public void EveryPythonClassHasAConstructorOfItsOwnAPresetsNamingItsLookup()
    {
        string dir = Path.Combine(Path.GetTempPath(), "nanoql-bind-python-" + Guid.NewGuid().ToString("N"));
        try
        {
            Surfaces.Nanoql.WritePython(dir);
            string stable = File.ReadAllText(Path.Combine(dir, "_generated.py"));
            string experimental = File.ReadAllText(Path.Combine(dir, "experimental", "__init__.py"));
            Assert.Contains("class USDCurrency(*_bases(Currency)):\n    __slots__ = ()\n    _type = \"NanoQL.Currencies.USDCurrency\"\n    _key = \"nql.USDCurrency\"\n    _sealed = True\n    __init__ = Object.__init__\n    _made_by = \"ConventionSet.currency\"\n", stable, StringComparison.Ordinal);

            // Each generated class body holds an __init__, its constructors' or the runtime's refusal.
            int classes = 0;
            foreach (string module in new[] { stable, experimental })
            {
                string[] bodies = module.Split("\nclass ", StringSplitOptions.None)[1..];
                foreach (string body in bodies.Where(b => b.Contains("    _type = ", StringComparison.Ordinal)))
                {
                    string head = body[..(body.IndexOf("\n\n\n", StringComparison.Ordinal) is int e and >= 0 ? e : body.Length)];
                    Assert.True(head.Contains("    __init__ = Object.__init__", StringComparison.Ordinal) || head.Contains("def __init__", StringComparison.Ordinal), body[..Math.Min(80, body.Length)]);
                    classes++;
                }
            }

            Assert.True(classes > 2000, $"{classes} classes");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
