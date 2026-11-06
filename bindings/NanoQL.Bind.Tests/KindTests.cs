// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Bind.Model;

namespace NanoQL.Bind.Tests;

/// <summary>
/// Each kind's C form (docs/bindings.md, "Kinds"), and the rules the prototypes found: a type's required members as
/// trailing constructor parameters, a date's and a period's own members taking the value, operators by their metadata
/// names, C and C++ keywords and the generated names escaped, deprecations and documented exceptions carried.
/// </summary>
public class KindTests
{
    private static string? Fx(string name) => Surfaces.Declaration(Surfaces.WithFixtures, name);

    [Fact]
    public void CollectionsCrossAsAPointerAndACount()
    {
        Assert.Equal("NQL_API nql_status fx_Kinds_Dates(const nql_date* x, int32_t x_count, nql_date** result, int32_t* result_count);", Fx("fx_Kinds_Dates"));
    }

    [Fact]
    public void ANullableDateIsTheSerialAndANullableDoubleAPointerAndAFlag()
    {
        Assert.Equal("NQL_API nql_status fx_Kinds_Nullables(nql_date d, const double* x, double* result, bool* result_has_value);", Fx("fx_Kinds_Nullables"));
    }

    [Fact]
    public void APeriodCrossesByValueAndAnEnumAsAnInteger()
    {
        Assert.Equal("NQL_API nql_status fx_Kinds_PeriodAndEnum(nql_period p, int32_t u, nql_period* result);", Fx("fx_Kinds_PeriodAndEnum"));
    }

    [Fact]
    public void AMatrixCrossesByRowsAndAVectorAsAnArray()
    {
        Assert.Equal(
            "NQL_API nql_status fx_Kinds_MatrixAndVector(const double* m, int32_t m_rows, int32_t m_columns, const double* v, int32_t v_count, double** result, int32_t* result_count);",
            Fx("fx_Kinds_MatrixAndVector"));
    }

    [Fact]
    public void AJaggedArrayIsItsValuesAndItsRowsLengths()
    {
        Assert.Equal(
            "NQL_API nql_status fx_Kinds_Jagged(const double* rows, int32_t rows_count, const int32_t* rows_row_lengths, int32_t rows_rows, double** result, int32_t** result_row_lengths, int32_t* result_rows);",
            Fx("fx_Kinds_Jagged"));
    }

    [Fact]
    public void ADictionaryIsItsKeysAndItsValues()
    {
        Assert.Equal(
            "NQL_API nql_status fx_Kinds_Map(const nql_date* m_keys, const double* m_values, int32_t m_count, nql_date** result_keys, double** result_values, int32_t* result_count);",
            Fx("fx_Kinds_Map"));
    }

    [Fact]
    public void ATupleIsAnOutParameterPerElementByItsName()
    {
        Assert.Equal("NQL_API nql_status fx_Kinds_Split(double x, int32_t* whole, double* fraction);", Fx("fx_Kinds_Split"));
    }

    [Fact]
    public void AComplexNumberIsAStruct()
    {
        Assert.Equal("NQL_API nql_status fx_Kinds_Conjugate(nql_complex z, nql_complex* result);", Fx("fx_Kinds_Conjugate"));
    }

    [Fact]
    public void KeywordsAndTheGeneratedNamesAreEscaped()
    {
        Assert.Equal("NQL_API nql_status fx_Kinds_Keywords(double default_, double result_, double self_, double* result);", Fx("fx_Kinds_Keywords"));
    }

    [Fact]
    public void ADeprecatedMemberIsDeclaredDeprecated()
    {
        Assert.Equal("NQL_DEPRECATED(\"use Kinds.Dates\") NQL_API nql_status fx_Kinds_Old(double x, double* result);", Fx("fx_Kinds_Old"));
    }

    [Fact]
    public void ADocumentedExceptionOutsideTheTableMapsByTheDocumentation()
    {
        Model.CFunction f = Surfaces.WithFixtures.Surface.Functions.Single(x => x.CName == "fx_Kinds_Element");
        Assert.Equal([("System.IndexOutOfRangeException", 4)], f.DocumentedCodes);
    }

    [Fact]
    public void ARequiredMemberIsATrailingConstructorParameter()
    {
        Assert.Equal("NQL_API nql_status nql_EvaluationContext_new(nql_date evaluationDate, nql_handle* result);", Surfaces.Declaration(Surfaces.Nanoql, "nql_EvaluationContext_new"));
    }

    [Fact]
    public void ADatesOwnMembersTakeItsValue()
    {
        Assert.Equal("NQL_API nql_status nql_Date_get_Year(nql_date self, int32_t* result);", Surfaces.Declaration(Surfaces.Nanoql, "nql_Date_get_Year"));
        Assert.NotNull(Surfaces.Declaration(Surfaces.Nanoql, "nql_Date_op_Addition__Date_int"));
    }

    [Fact]
    public void TodaysDateReadsTheComputersClock()
    {
        Assert.Equal("NQL_API nql_status nql_Date_TodaysDate(nql_date* result);", Surfaces.Declaration(Surfaces.Nanoql, "nql_Date_TodaysDate"));
    }

    [Fact]
    public void AStaticSetterCrosses()
    {
        Assert.Equal("NQL_API nql_status nql_ConventionProvider_set_Default(nql_handle value);", Surfaces.Declaration(Surfaces.Nanoql, "nql_ConventionProvider_set_Default"));
    }

    [Fact]
    public void AGenericMethodOverNamedFactoriesClosesPerFactory()
    {
        Assert.NotNull(Surfaces.Declaration(Surfaces.Nanoql, "nql_BlackVarianceCurve_SetInterpolation__Linear"));
        Assert.NotNull(Surfaces.Declaration(Surfaces.Nanoql, "nql_BlackVarianceCurve_SetInterpolation__Cubic"));
    }

    [Fact]
    public void AnExperimentalDeclarationIsCompiledOnlyUnderTheOptIn()
    {
        // Every function's declaration lies inside an #ifdef NANOQL_EXPERIMENTAL block exactly when it is experimental.
        bool inside = false;
        var experimental = Surfaces.Nanoql.Surface.Functions.ToDictionary(f => f.CName, f => f.Experimental, StringComparer.Ordinal);
        int seen = 0, wrong = 0;
        foreach (string line in Surfaces.Nanoql.Files["c/include/nanoql.h"].Split('\n'))
        {
            if (line.StartsWith("#ifdef NANOQL_EXPERIMENTAL", StringComparison.Ordinal))
            {
                inside = true;
            }
            else if (line.StartsWith("#endif /* NANOQL_EXPERIMENTAL */", StringComparison.Ordinal))
            {
                inside = false;
            }
            else if (line.Contains("NQL_API nql_status nql_", StringComparison.Ordinal))
            {
                // after a deprecated declaration's NQL_DEPRECATED("...")
                int start = line.IndexOf("nql_status ", StringComparison.Ordinal) + 11;
                string name = line[start..line.IndexOf('(', start)];
                if (experimental.TryGetValue(name, out bool e))
                {
                    seen++;
                    wrong += e == inside ? 0 : 1;
                }
            }
        }

        Assert.Equal(Surfaces.Nanoql.Surface.Functions.Count, seen);
        Assert.Equal(0, wrong);
        Assert.Contains(Surfaces.Nanoql.Surface.Functions, f => f.Experimental);
    }

    [Fact]
    public void TheHeaderCarriesTheVersionAndItsCheck()
    {
        string header = Surfaces.Nanoql.Files["c/include/nanoql.h"];
        Assert.Contains("#define NQL_VERSION \"2.1\"", header, StringComparison.Ordinal);
        Assert.Contains("#define NQL_ABI_VERSION 2001", header, StringComparison.Ordinal);
        Assert.Contains("assert(nql_abi_version() == NQL_ABI_VERSION);", header, StringComparison.Ordinal);
    }

    [Fact]
    public void AGenericMethodWithOneClassOrInterfaceConstraintClosesAtIt()
    {
        // R14: Calibrate<TModel> where TModel : ICalibratedModel takes any calibrated model, and its result, a closed
        // generic type, crosses with its members under its definition's and argument's names.
        Assert.NotNull(Surfaces.Declaration(Surfaces.Nanoql, "nql_CalibratedModelExtensions_Calibrate"));
        Assert.NotNull(Surfaces.Declaration(Surfaces.Nanoql, "nql_CalibrationResults_ICalibratedModel_get_Model"));
        Assert.NotNull(Surfaces.Declaration(Surfaces.Nanoql, "nql_CalibrationResults_Gsr_get_Model"));
        Assert.Contains(Surfaces.Nanoql.Surface.Types, t => t.CName == "CalibrationResults_Gsr" && t.Type.IsConstructedGenericType);
        Assert.Contains("nql_handle x, nql_handle* result", Surfaces.Declaration(Surfaces.WithFixtures, "fx_GenericMembers_Same"), StringComparison.Ordinal);
        Assert.Null(Surfaces.Declaration(Surfaces.WithFixtures, "fx_GenericMembers_Make"));
        Assert.Contains(Surfaces.WithFixtures.Surface.Skips, s => s.Display.StartsWith("GenericMembers.Make", StringComparison.Ordinal) && s.Reason == "generic method");
    }

    [Fact]
    public void AMatrixOrVectorItsMemberWritesCrossesInAndOut()
    {
        // R15: double* with its count, the shim copying the C# vector back; an argument only read stays const.
        Assert.Equal("NQL_API nql_status nql_Constraint_Update(nql_handle self, double* p, int32_t p_count, const double* direction, int32_t direction_count, double beta, double* result);",
            Surfaces.Declaration(Surfaces.Nanoql, "nql_Constraint_Update"));
        Assert.All(Surfaces.Nanoql.Surface.Written, w => Assert.EndsWith("&", w.Parameter.Kind.Name, StringComparison.Ordinal));
        Assert.Equal(Surfaces.Nanoql.Surface.Written.Count, Surfaces.Nanoql.Surface.Functions.Sum(f => f.Params.Count(p => p.Kind.Name is "vector&" or "matrix&")));
    }

    [Fact]
    public void ATypesBaseIsTheNearestThatCrossesAClosedConstructionIncluded()
    {
        // CdsHelper's own base, RelativeDateBootstrapHelper<DefaultProbabilityTermStructure>, doesn't cross; its base
        // crosses as a closed construction, so a CdsHelper goes wherever that class is taken.
        TypeRow cds = Surfaces.Nanoql.Surface.Types.Single(t => t.CName == "CdsHelper");
        TypeRow helper = Surfaces.Nanoql.Surface.Types.Single(t => t.CName == "BootstrapHelper_DefaultProbabilityTermStructure");
        Assert.Equal(helper.Type.FullName, cds.Base);
        Assert.Equal("NanoQL.TermStructures.YieldTermStructure", Surfaces.Nanoql.Surface.Types.Single(t => t.CName == "FlatForward").Base);
    }
}
