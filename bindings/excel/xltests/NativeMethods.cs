// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;

namespace NanoQL.Excel.XlTests;

/// <summary>The one Win32 call the suite makes: the process behind an Excel instance's window.</summary>
internal static class NativeMethods
{
    /// <summary>The process that owns a window.</summary>
    /// <param name="hwnd">The window.</param>
    /// <param name="pid">The process's identifier.</param>
    /// <returns>The thread that created the window.</returns>
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
}
