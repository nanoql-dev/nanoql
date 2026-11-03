// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace NanoQL.Excel.XlTests;

/// <summary>
/// The add-ins run.py builds into <c>obj/addins/</c> with <c>nanoql-bind excel build</c>: the two shipped builds (the
/// list on), the test build (the experimental one with the bindings' test types and <c>nqlxlStats</c>), whose functions
/// the runner's instance loads, a user's build, the experimental tests' build the replayer reads (<c>texp</c>,
/// compiled, no <c>.xll</c>), and the package's stable add-in.
/// </summary>
internal static class AddIns
{
    /// <summary>The test build for ExcelDna.Testing's <c>AddIn</c>: relative to the test assembly's folder, without "64.xll".</summary>
    public const string Test = @"..\..\..\obj\addins\test\nanoql-nqlt-experimental";

    /// <summary>Gets the shipped experimental build's <c>.xll</c>.</summary>
    public static string Experimental => Xll("experimental", "nanoql-experimental");

    /// <summary>Gets the shipped stable build's <c>.xll</c>.</summary>
    public static string Stable => Xll("stable", "nanoql");

    /// <summary>Gets the test build's <c>.xll</c>.</summary>
    public static string TestXll => Xll("test", "nanoql-nqlt-experimental");

    /// <summary>Gets the user's build's <c>.xll</c>: nanoql's listed stable functions and the user's assembly's.</summary>
    public static string Acme => Xll("acme", "nanoql-acme");

    /// <summary>Gets the stable <c>.xll</c> run.py extracted from <c>excel package</c>'s zip.</summary>
    public static string Package => Xll("package", "nanoql");

    /// <summary>Gets the vectors' replayer, which run.py builds.</summary>
    public static string Replayer => Path.Combine(Repository, "bindings", "excel", "replay", "bin", "Release", "net10.0-windows", "NanoQL.Excel.Replay.dll");

    /// <summary>Gets the file in which run.py names the Python interpreter its --python gave, with nanoql's Python package and NumPy.</summary>
    public static string PythonFile => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "obj", "python.txt"));

    /// <summary>Gets the repository's root.</summary>
    public static string Repository { get; } = FindRepository();

    /// <summary>A build's folder.</summary>
    /// <param name="build">The build: experimental, stable, test, acme, texp or package.</param>
    /// <returns>Its absolute path.</returns>
    public static string Folder(string build) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "obj", "addins", build));

    /// <summary>A build's surface, the <c>functions.json</c> the tool wrote beside it.</summary>
    /// <param name="build">The build.</param>
    /// <returns>Its parsed text.</returns>
    public static JsonDocument Manifest(string build) => JsonDocument.Parse(File.ReadAllText(Path.Combine(Folder(build), "functions.json")));

    /// <summary>The checked-in surface, the shipped experimental build's.</summary>
    /// <returns>Its parsed text.</returns>
    public static JsonDocument CheckedIn() => JsonDocument.Parse(File.ReadAllText(Path.Combine(Repository, "bindings", "excel", "functions.json")));

    /// <summary>A fixture's path in the test assembly's folder.</summary>
    /// <param name="name">The fixture's file name.</param>
    /// <returns>Its path.</returns>
    public static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    /// <summary>A new, empty folder for a test's files, under the suite's <c>obj/scratch/</c>, which run.py clears.</summary>
    /// <returns>Its path.</returns>
    public static string Scratch()
    {
        string dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "obj", "scratch", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Xll(string build, string name) => Path.Combine(Folder(build), name + "64.xll");

    private static string FindRepository()
    {
        for (string? d = AppContext.BaseDirectory; d != null; d = Path.GetDirectoryName(d))
        {
            if (File.Exists(Path.Combine(d, "bindings", "roles.json")))
            {
                return d;
            }
        }

        throw new InvalidOperationException("the repository (bindings/roles.json) isn't above the test assembly");
    }
}
