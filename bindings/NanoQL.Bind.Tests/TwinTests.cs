// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Bind.Emitting;

namespace NanoQL.Bind.Tests;

/// <summary>
/// The recorder's twins (docs/bindings.md, "The vector suite through each binding"): their map, which the generation
/// reads back from the folder it was just written into.
/// </summary>
public class TwinTests
{
    [Fact]
    public void TheMapIsClosedAndWholeOnceWritten()
    {
        string dir = Path.Combine(Path.GetTempPath(), "nanoql-bind-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string map = Path.Combine(dir, "twins.json");
            TwinWriter.WriteMap(map, [("M:NanoQL.Time.Date.#ctor(System.Int32)", "F0", "newobj"), ("M:NanoQL.Time.Date.get_Year", "F1", "call")]);

            // An open stream is refused by the exclusive open: Windows' share mode, .NET's advisory lock elsewhere.
            using (File.Open(map, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
            }

            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(map));
            Assert.Equal(["F0", "newobj"], doc.RootElement.GetProperty("M:NanoQL.Time.Date.#ctor(System.Int32)").EnumerateArray().Select(e => e.GetString()));
            Assert.Equal(2, doc.RootElement.EnumerateObject().Count());
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
