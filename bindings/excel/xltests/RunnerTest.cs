// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using ExcelDna.Testing;
using Microsoft.Office.Interop.Excel;

namespace NanoQL.Excel.XlTests;

/// <summary>
/// A test in the runner's Excel instance. Each test makes its workbooks itself and closes them after it, every call
/// retried while Excel is busy, which ExcelDna.Testing's own workbook per test isn't: right after a test closed a workbook
/// whose handles Excel was releasing, the next test's workbook could meet a busy Excel and fail before its first line.
/// </summary>
public abstract class RunnerTest : IDisposable
{
    private readonly List<Workbook> books = [];

    /// <summary>Closes the test's workbooks, unsaved.</summary>
    public void Dispose()
    {
        foreach (Workbook wb in books)
        {
            Xl.Retry(() => wb.Close(false));
        }

        books.Clear();
        GC.SuppressFinalize(this);
    }

    /// <summary>A new workbook's first worksheet, in the runner's instance.</summary>
    /// <returns>The worksheet.</returns>
    protected Worksheet NewSheet()
    {
        Workbook wb = Xl.Retry(() => Util.Application.Workbooks.Add());
        books.Add(wb);
        return Xl.Sheet(wb);
    }
}
