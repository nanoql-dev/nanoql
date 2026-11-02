// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using ExcelDna.Integration;
using static NanoQL.Excel.Tests.Xl;

namespace NanoQL.Excel.Tests;

/// <summary>
/// The errors (XLR4): an exception to BIR4's code, the runtime's own kinds first, then the codes a member documents,
/// then the runtime's copy of the native host's table, checked against S-bind's by reflection; each code to its Excel
/// error; the message by its cell.
/// </summary>
[Collection(TestSettings.Name)]
public sealed class ErrorTests
{
    /// <summary>Initializes a new instance of the <see cref="ErrorTests"/> class, on a clean runtime.</summary>
    public ErrorTests() => Reset();

    /// <summary>
    /// The runtime's copy of S-bind's <c>Rt.Code</c> agrees with S-bind's own over every exception type the runtime's
    /// assemblies define, the BCL's and nanoql's, every assembly S-bind's library references loaded first (F3-5, F5-3),
    /// read from the managed library B2's native build leaves (F6-2); a missing one fails.
    /// </summary>
    [Fact]
    public void TheCopyOfTheCodeTableIsSBinds()
    {
        string path = Path.Combine(Repository(), "src", "NanoQL.Native", "bin", "Release", "net10.0", "win-x64", "NanoQL.Native.dll");
        Assert.True(File.Exists(path), $"S-bind's managed library isn't at {path}: run the native build (B2) first");
        Assembly native = Assembly.LoadFrom(path);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void LoadReferences(Assembly a)
        {
            foreach (AssemblyName r in a.GetReferencedAssemblies())
            {
                if (seen.Add(r.FullName))
                {
                    try
                    {
                        LoadReferences(Assembly.Load(r));
                    }
                    catch (IOException)
                    {
                    }
                }
            }
        }

        LoadReferences(native);
        LoadReferences(typeof(X).Assembly);
        _ = typeof(System.Xml.XmlException);
        _ = typeof(System.Xml.Schema.XmlSchemaValidationException);
        MethodInfo code = native.GetTypes().Single(t => t.Name == "Rt").GetMethod("Code", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, [typeof(Exception)])!;
        var kinds = AppDomain.CurrentDomain.GetAssemblies().Where(a => a != native && !a.IsDynamic)
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    return e.Types.Where(t => t != null).Select(t => t!);
                }
            })
            .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic && !t.IsGenericTypeDefinition && typeof(Exception).IsAssignableFrom(t)).Distinct().ToList();
        var differ = new List<string>();
        int compared = 0;
        foreach (Type t in kinds)
        {
            Exception? e = null;
            try
            {
                e = t.GetConstructor(Type.EmptyTypes) != null ? (Exception)Activator.CreateInstance(t)!
                    : t.GetConstructor([typeof(string)]) != null ? (Exception)Activator.CreateInstance(t, "x")! : null;
            }
            catch (TargetInvocationException)
            {
            }

            if (e == null)
            {
                continue;
            }

            compared++;
            int theirs = (int)code.Invoke(null, [e])!, ours = X.CopyOfRtCode(e);
            if (theirs != ours)
            {
                differ.Add($"{t.FullName}: S-bind's {theirs}, the copy's {ours}");
            }
        }

        Assert.True(compared > 100, $"{compared} exception types compared");
        Assert.Empty(differ);
    }

    /// <summary>Each code to XLR4's Excel error: refused arguments, kinds, casts, names, files #VALUE!; a handle #REF!; the rest #NUM!.</summary>
    [Fact]
    public void EachCodeTakesItsExcelError()
    {
        foreach (int c in new[] { 2, 3, 4, 6, 7, 8, 10, 12, 13 })
        {
            Assert.Equal(ExcelError.ExcelErrorValue, X.ExcelOf(c));
        }

        Assert.Equal(ExcelError.ExcelErrorRef, X.ExcelOf(9));
        foreach (int c in new[] { 1, 5, 11, 99 })
        {
            Assert.Equal(ExcelError.ExcelErrorNum, X.ExcelOf(c));
        }

        Assert.Equal(13, X.BirCode(new System.Xml.XmlException("x")));
        Assert.Equal(ExcelError.ExcelErrorValue, X.ExcelOf(X.BirCode(new System.Xml.XmlException("x"))));
    }

    /// <summary>The runtime's own kinds first: an unknown handle #REF!, though it is an argument exception; a failed settings load #VALUE!.</summary>
    [Fact]
    public void TheRuntimesOwnKindsComeFirst()
    {
        Assert.Equal(9, X.BirCode(new XlHandleException("x", "p")));
        Assert.Equal(2, X.BirCode(new XlSettingsException("x")));
        Assert.Equal(ExcelError.ExcelErrorRef, Call("nqlYieldTermStructureDiscount2", "FlatForward#0123456789ABCDEF", 1.0));
        Assert.Contains("no object \"FlatForward#0123456789ABCDEF\" in this Excel session", Message(), StringComparison.Ordinal);
    }

    /// <summary>A documented kind outside the table takes its documented code (#VALUE!), an undocumented one 99 (#NUM!) (F4-2).</summary>
    [Fact]
    public void ADocumentedKindTakesItsCode()
    {
        Assert.Equal(ExcelError.ExcelErrorValue, Call("nqltRefusalsDocumentedElement", 5.0));
        Assert.Equal(ExcelError.ExcelErrorNum, Call("nqltRefusalsUndocumentedElement", 5.0));
    }

    /// <summary>An unknown convention name #VALUE!, its message naming it; QuantLib's own check #NUM!, its message by the cell.</summary>
    [Fact]
    public void AMessageByItsCell()
    {
        Assert.Equal(ExcelError.ExcelErrorValue, Call("nqlConventionSetCalendar", null, "NoSuchCalendar"));
        Assert.Contains("NoSuchCalendar", Message(), StringComparison.Ordinal);
        string dc = H(Call("nqlConventionSetDayCounter", null, "Actual365Fixed"));
        string curve = H(Call("nqlFlatForward", 42262.0, 0.03, dc));
        Host.Cell = TestHost.At(3, 1);
        Assert.Equal(ExcelError.ExcelErrorNum, Call("nqlYieldTermStructureDiscount2", curve, -1.0));
        Assert.Contains("negative time", Message(), StringComparison.Ordinal);
        Host.Show(3, 1, ExcelError.ExcelErrorNum);
        Assert.Contains("negative time", (string)OwnFunctions.Error(TestHost.At(3, 1)), StringComparison.Ordinal);
        Host.Show(3, 1, 0.5);
        Assert.Equal(string.Empty, OwnFunctions.Error(TestHost.At(3, 1)));
    }

    /// <summary>The repository: the nearest folder above the tests holding bindings/roles.json.</summary>
    /// <returns>Its path.</returns>
    internal static string Repository()
    {
        for (string? d = AppContext.BaseDirectory; d != null; d = Path.GetDirectoryName(d))
        {
            if (File.Exists(Path.Combine(d, "bindings", "roles.json")))
            {
                return d;
            }
        }

        throw new DirectoryNotFoundException("no bindings/roles.json above the tests");
    }
}
