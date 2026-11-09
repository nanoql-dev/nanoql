// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using System.Text;

namespace NanoQL.Excel.XlTests;

/// <summary>
/// The Win32 calls the suite makes: the process behind an Excel instance's window, and a process's visible windows by
/// their title.
/// </summary>
internal static class NativeMethods
{
    // A window as EnumWindows hands it over, and whether to go on.
    private delegate bool EnumWindowsProc(nint hwnd, nint lParam);

    /// <summary>The process that owns a window.</summary>
    /// <param name="hwnd">The window.</param>
    /// <param name="pid">The process's identifier.</param>
    /// <returns>The thread that created the window.</returns>
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);

    /// <summary>The title of a visible top-level window of a process that ends with a text, or null.</summary>
    /// <param name="pid">The process's identifier.</param>
    /// <param name="suffix">The title's end.</param>
    /// <returns>The first such window's title, or null when none shows.</returns>
    public static string? VisibleWindowTitled(int pid, string suffix)
    {
        string? found = null;
        EnumWindows(
            (hwnd, _) =>
            {
                if (!IsWindowVisible(hwnd) || GetWindowThreadProcessId(hwnd, out uint owner) == 0 || owner != (uint)pid)
                {
                    return true;
                }

                var title = new StringBuilder(512);
                if (GetWindowText(hwnd, title, title.Capacity) > 0 && title.ToString().EndsWith(suffix, StringComparison.Ordinal))
                {
                    found = title.ToString();
                    return false;
                }

                return true;
            },
            0);
        return found;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hwnd, StringBuilder text, int count);
}
