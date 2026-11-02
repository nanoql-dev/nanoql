// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Microsoft.Office.Interop.Excel;
using Action = System.Action;

namespace NanoQL.Excel.XlTests;

/// <summary>
/// An Excel instance of a test's own, for what the runner's shared instance can't hold: a build of its own (the shipped
/// builds, a user's), a settings file read at load, a workbook reopened in a new instance. Started through automation
/// (a new process, never the user's), the add-in loaded for the session alone with <c>RegisterXLL</c>, and quit; the
/// instance must then end by itself, every object the add-in made released (XLP79), or the test fails, its process
/// killed by its identifier alone.
/// </summary>
internal static class OwnExcel
{
    /// <summary>Runs a test's body in a new Excel instance with an add-in loaded.</summary>
    /// <param name="xll">The add-in, or null for none.</param>
    /// <param name="body">The body.</param>
    public static void Run(string? xll, Action<Application> body)
    {
        (int pid, ExceptionDispatchInfo? failed) = Start(xll, body);
        WaitForExit(pid, failed != null);
        failed?.Throw();
    }

    // The instance's references live in this frame alone, so they are unreachable once it returns.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (int Pid, ExceptionDispatchInfo? Failed) Start(string? xll, Action<Application> body)
    {
        var before = Process.GetProcessesByName("EXCEL").Select(p => p.Id).ToHashSet();
        var app = new Application();
        NativeMethods.GetWindowThreadProcessId(Xl.Retry(() => (nint)app.Hwnd), out uint id);
        int pid = (int)id;
        ExceptionDispatchInfo? failed = null;
        try
        {
            Assert.DoesNotContain(pid, before);
            Xl.Retry(() => app.DisplayAlerts = false);
            if (xll != null)
            {
                Assert.True(File.Exists(xll), $"{xll}: no add-in (run.py builds it)");
                Assert.True(Xl.Retry(() => app.RegisterXLL(xll)), $"RegisterXLL({xll})");
            }

            body(app);
        }
        catch (Exception e)
        {
            failed = ExceptionDispatchInfo.Capture(e);
        }

        try
        {
            foreach (Workbook wb in Xl.Retry(() => app.Workbooks.Cast<Workbook>().ToList()))
            {
                Xl.Retry(() => wb.Close(false));
            }

            Xl.Retry(app.Quit);
        }
        catch (Exception e) when (failed == null)
        {
            failed = ExceptionDispatchInfo.Capture(e);
        }

        return (pid, failed);
    }

    private static void WaitForExit(int pid, bool failing)
    {
        for (int i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Process p;
        try
        {
            p = Process.GetProcessById(pid);
        }
        catch (ArgumentException)
        {
            return;
        }

        using (p)
        {
            if (!p.WaitForExit(TimeSpan.FromSeconds(60)))
            {
                p.Kill();
                p.WaitForExit();
                if (!failing)
                {
                    Assert.Fail($"Excel (process {pid}) didn't end by itself after Quit; killed");
                }
            }
        }
    }
}
