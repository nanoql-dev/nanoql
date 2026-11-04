// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Tests;

/// <summary>
/// The tool outside any repository (docs/bindings.md, the tool), in a scratch folder with no <c>bindings/roles.json</c>
/// above it: the commands that work on the repository's own files, and those writing the C++ and Java layers, refuse;
/// <c>report</c> reads the defaults beside the tool; <c>build</c> writes the native host into its output, publishes it
/// against the nanoql assemblies the generation reads (the tool's own with none given, else the two given, from one
/// folder), and writes the C headers alone, its publish's command line captured.
/// </summary>
public class OutsideRepositoryTests
{
    [Fact]
    public void TheRepositorysCommandsAndTheUnreleasedLayersRefuse()
    {
        string dir = Scratch();
        try
        {
            string[][] refused =
            [
                ["update"], ["check"], ["freeze"], ["headers", "--out", Path.Combine(dir, "h"), "--test-exports"],
                ["cpp", "--out", Path.Combine(dir, "c")], ["java", "--out", Path.Combine(dir, "j")],
                ["replay-cpp", "--out", Path.Combine(dir, "rc")], ["replay-java", "--out", Path.Combine(dir, "rj")],
                ["excel", "update"], ["excel", "check"], ["excel", "package", "--out", Path.Combine(dir, "p")],
                ["excel", "generate", "--out", Path.Combine(dir, "g"), "--test-exports"],
            ];
            foreach (string[] args in refused)
            {
                var e = Assert.Throws<IOException>(() => Program.Run(args, dir, Captured([])));
                Assert.EndsWith("run nanoql-bind in the repository", e.Message, StringComparison.Ordinal);
            }

            Assert.Empty(Directory.EnumerateFileSystemEntries(dir));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ReportReadsTheDefaults()
    {
        string dir = Scratch();
        try
        {
            Assert.True(File.Exists(Path.Combine(Program.Defaults(), "bindings", "roles.json")));
            Assert.Equal(0, Program.Run(["report"], dir, Captured([])));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void BuildWritesTheHostAndTheCHeadersAlone()
    {
        string dir = Scratch();
        try
        {
            string output = Path.Combine(dir, "out");
            var calls = new List<IReadOnlyList<string>>();
            Assert.Equal(0, Program.Run(["build", "--rid", "win-x64", "--out", output], dir, Captured(calls)));
            IReadOnlyList<string> argv = Assert.Single(calls);
            string host = Path.Combine(output, "host", "NanoQL.Native.csproj");
            Assert.Equal(["publish", host], argv.Take(2));
            Assert.Contains("-p:NanoQLLibraryDir=" + AppContext.BaseDirectory, argv);
            Assert.True(File.Exists(host));
            Assert.True(File.Exists(Path.Combine(output, "host", "Rt.cs")));
            Assert.Equal("<Project />\n", File.ReadAllText(Path.Combine(output, "host", "Directory.Build.props")));
            Assert.Equal(["nanoql.h"], Directory.EnumerateFiles(Path.Combine(output, "include")).Select(f => Path.GetFileName(f)));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void BuildCompilesAgainstTheNanoqlCopiesItReads()
    {
        string dir = Scratch();
        try
        {
            string lib = Path.Combine(dir, "lib");
            Directory.CreateDirectory(lib);
            foreach (string f in new[] { "NanoQL.dll", "NanoQL.Extensions.dll" })
            {
                File.Copy(Path.Combine(AppContext.BaseDirectory, f), Path.Combine(lib, f));
            }

            var calls = new List<IReadOnlyList<string>>();
            string[] args = ["build", "--rid", "win-x64", "--out", Path.Combine(dir, "out"),
                "--assembly", "nql=" + Path.Combine(lib, "NanoQL.dll"), "--assembly", "nql=" + Path.Combine(lib, "NanoQL.Extensions.dll")];
            Assert.Equal(0, Program.Run(args, dir, Captured(calls)));
            Assert.Contains("-p:NanoQLLibraryDir=" + lib + Path.DirectorySeparatorChar, Assert.Single(calls));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void NanoqlCopiesFromTwoFoldersOrOneAloneRefuse()
    {
        string dir = Scratch();
        try
        {
            string lib = Path.Combine(dir, "lib");
            Directory.CreateDirectory(lib);
            File.Copy(Path.Combine(AppContext.BaseDirectory, "NanoQL.dll"), Path.Combine(lib, "NanoQL.dll"));
            string[][] refused =
            [
                ["--assembly", "nql=" + Path.Combine(lib, "NanoQL.dll"), "--assembly", "nql=" + Path.Combine(AppContext.BaseDirectory, "NanoQL.Extensions.dll")],
                ["--assembly", "nql=" + Path.Combine(lib, "NanoQL.dll")],
            ];
            foreach (string[] given in refused)
            {
                var e = Assert.Throws<ArgumentException>(() => Program.Run(["build", "--rid", "win-x64", "--out", Path.Combine(dir, "out"), .. given], dir, Captured([])));
                Assert.Contains("both from one folder", e.Message, StringComparison.Ordinal);
            }

            Assert.False(Directory.Exists(Path.Combine(dir, "out")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    // A process runner that runs nothing: it records each call's arguments and makes the publish folder a publish
    // would, so the build goes on to copy the library's files from it, of which there are none.
    private static Func<string, IReadOnlyList<string>, int> Captured(List<IReadOnlyList<string>> calls) => (file, argv) =>
    {
        Assert.Equal("dotnet", file);
        calls.Add(argv);
        Directory.CreateDirectory(argv[argv.ToList().IndexOf("-o") + 1]);
        return 0;
    };

    private static string Scratch()
    {
        string dir = Path.Combine(Path.GetTempPath(), "nanoql-bind-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        for (string? d = dir; d != null; d = Path.GetDirectoryName(d))
        {
            Assert.False(File.Exists(Path.Combine(d, "bindings", "roles.json")), d + " holds bindings/roles.json");
        }

        return dir;
    }
}
