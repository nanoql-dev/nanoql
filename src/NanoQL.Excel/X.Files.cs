// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Extensions.Conventions;

namespace NanoQL.Excel;

/// <summary>
/// The file readers (XLR3): a call reading files keys their content beside the paths' text; the single-path read parses
/// the very bytes the key hashed, and the multi-path read hashes again after it reads and refuses a file changed in
/// between. A path in a cell must be absolute (XLD7).
/// </summary>
public static partial class X
{
    // The bytes a reader's key hashed, by path, for the read on the same thread.
    [ThreadStatic]
    private static Dictionary<string, byte[]>? readBytes;

    /// <summary>Gets or sets a hook the multi-path read's test calls between its read and its second hash.</summary>
    internal static Action? BetweenReadAndCheck { get; set; }

    /// <summary>Runs a call that reads files: keyed by their content too, its read parsing the bytes the key hashed.</summary>
    /// <param name="fn">The worksheet function's name.</param>
    /// <param name="args">The Excel arguments.</param>
    /// <param name="body">The call, on converted arguments.</param>
    /// <param name="lift">The loop mask, or null.</param>
    /// <returns>The handle, or Excel's error.</returns>
    public static object RunReader(string fn, object[] args, Func<object[], object?> body, string? lift = null)
        => lift != null && LiftShape(args = Cells1(args, lift), lift, out int rows, out int cols)
            ? Lifted(fn, args, lift, rows, cols, e => RunReaderOne(fn, e, body))
            : RunReaderOne(fn, args, body);

    /// <summary>A path, which must be absolute: Excel's current folder isn't the workbook's (XLD7).</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The path.</returns>
    public static string AbsPath(object a, string p)
    {
        string s = Str(a, p);
        return Path.IsPathFullyQualified(s) ? s : throw new ArgumentException($"{p}: \"{s}\" is a relative path; give the file's full path", p);
    }

    /// <summary>The bytes of the file the key hashed, as a stream for <c>ConventionsXml.Read(Stream, source)</c>.</summary>
    /// <param name="a">The Excel value: the path.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The stream.</returns>
    public static Stream HashedFile(object a, string p)
    {
        string path = AbsPath(a, p);
        return new MemoryStream(readBytes != null && readBytes.TryGetValue(path, out byte[]? b) ? b : File.ReadAllBytes(path), false);
    }

    /// <summary>The multi-path read: it reads the files, then refuses one whose bytes changed since its key hashed them.</summary>
    /// <param name="a">The Excel value: the paths.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The snapshot.</returns>
    public static ConventionSnapshot ReadChecked(object a, string p)
    {
        string[] paths = Strs(a, p).Select(s => AbsPath(s, p)).ToArray();
        ConventionSnapshot snapshot = ConventionsXml.Read(paths);
        BetweenReadAndCheck?.Invoke();
        foreach (string path in paths)
        {
            if (readBytes == null || !readBytes.TryGetValue(path, out byte[]? b) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(b))
            {
                throw new ArgumentException($"{p}: {path} changed while it was read; recalculate the cell", p);
            }
        }

        return snapshot;
    }

    private static object RunReaderOne(string fn, object[] args, Func<object[], object?> body)
    {
        Interlocked.Increment(ref calls);
        try
        {
            if (Array.Find(args, a => a is ExcelDna.Integration.ExcelError) is ExcelDna.Integration.ExcelError passed)
            {
                KeepCall(fn, args, new XlPassed(passed));
                return passed;
            }

            object r = Wrap(fn, args, body, reads: true);
            KeepCall(fn, args, r);
            return r;
        }
        catch (Exception e)
        {
            return Fail(fn, args, e, null);
        }
    }

    // Each absolute path among the arguments that names a file: its bytes, hashed into the key.
    private static Dictionary<string, byte[]> KeyFiles(object[] args, MemoryStream ms)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (object a in args)
        {
            foreach (object x in a is object[,] m ? m.Cast<object>() : [a])
            {
                if (x is string path && Path.IsPathFullyQualified(path) && File.Exists(path) && !files.ContainsKey(path))
                {
                    byte[] b = File.ReadAllBytes(path);
                    files[path] = b;
                    ms.Write(System.Security.Cryptography.SHA256.HashData(b));
                }
            }
        }

        return files;
    }

    private static object? WithFiles(Dictionary<string, byte[]>? files, Func<object?> body)
    {
        Dictionary<string, byte[]>? outer = readBytes;
        readBytes = files;
        try
        {
            return body();
        }
        finally
        {
            readBytes = outer;
        }
    }
}
