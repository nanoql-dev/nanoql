// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// Runs every example and writes each output as a Markdown table, <name>-output.md, into the folder given:
// dotnet run --project docs/examples -c Release -- <folder>. The docs include each table beside its code.
using NanoQL.Examples;

if (args.Length != 1)
{
    Console.Error.WriteLine("usage: NanoQL.Examples <output folder>");
    return 2;
}

Directory.CreateDirectory(args[0]);
int written = 0;
foreach (var (page, outputs) in Examples.All)
{
    foreach (var (name, table) in outputs())
    {
        File.WriteAllText(Path.Combine(args[0], name + "-output.md"), table.ToMarkdown());
        written++;
    }
}

Console.WriteLine($"{written} outputs written to {args[0]}");
return 0;
