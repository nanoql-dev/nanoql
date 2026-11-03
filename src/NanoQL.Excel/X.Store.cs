// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ExcelDna.Integration;

namespace NanoQL.Excel;

/// <summary>The store: each object keyed by its call, built once per key, shown as its handle.</summary>
public static partial class X
{
    // By the key's whole hash; by the handle's text; a value call's elements under their parent's entry.
    private static readonly ConcurrentDictionary<string, Lazy<XlEntry>> ByKey = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, XlEntry> ByHandle = new(StringComparer.Ordinal);

    // The call presenting its result, so an object inside it is keyed by the call and its place.
    [ThreadStatic]
    private static XlParent? parent;

    // A tuple's or a map's slot, inside the call presenting it.
    [ThreadStatic]
    private static string? slotPath;

    /// <summary>Gets the effective convention provider's identity, part of every key: QuantLib's, a file's hash, or a failed load's.</summary>
    internal static string ProviderId { get; set; } = "QuantLib";

    /// <summary>Gets or sets the key's hash: SHA-256, which a test replaces to force a collision of the shown digits.</summary>
    internal static Func<byte[], byte[]> Hash { get; set; } = SHA256.HashData;

    /// <summary>Gets the live handles: the objects the store holds, an array's elements included.</summary>
    internal static int Count => ByHandle.Count;

    /// <summary>Gets whether a text is a handle this session holds.</summary>
    /// <param name="s">The text.</param>
    /// <returns>Whether the store holds it.</returns>
    internal static bool IsHandle(string s) => ByHandle.ContainsKey(s);

    /// <summary>Empties the store: the replayer and the tests, between runs.</summary>
    internal static void Clear()
    {
        ByKey.Clear();
        ByHandle.Clear();
        Errors.Clear();
        Spills.Clear();
        ByCell.Clear();
    }

    /// <summary>A handle's recipe: its function and its Excel arguments, for <c>nqlxlRecipe</c>.</summary>
    /// <param name="handle">The handle's text.</param>
    /// <returns>The recipe's text, or none when the store doesn't hold it.</returns>
    internal static string? RecipeText(string handle)
    {
        if (!ByHandle.TryGetValue(handle, out XlEntry? en))
        {
            return null;
        }

        return en.Item >= 0
            ? $"{en.ParentFn}({string.Join(", ", en.ParentArgs!.Select(Show))}), item {en.Item.ToString(CultureInfo.InvariantCulture)}"
            : $"{en.Function}({string.Join(", ", en.Args?.Select(Show) ?? [])})";
    }

    /// <summary>The entry behind a handle, for the tests and the export.</summary>
    /// <param name="handle">The handle's text.</param>
    /// <param name="entry">The entry.</param>
    /// <returns>Whether the store holds it.</returns>
    internal static bool TryEntry(string handle, out XlEntry entry) => ByHandle.TryGetValue(handle, out entry!);

    // A call that returns an object: keyed by its function, the provider, the date system and its arguments (a handle by
    // its text, which is its own key), built once per key and shown as its handle.
    private static object Wrap(string fn, object[] args, Func<object[], object?> body, bool reads = false)
    {
        byte[] recipe = Recipe(fn, args);
        Dictionary<string, byte[]>? files = null;
        if (reads)
        {
            // A reader's key holds the files' content beside the paths' text, so an edited file is a new object.
            using var ms = new MemoryStream();
            ms.Write(recipe);
            files = KeyFiles(args, ms);
            recipe = ms.ToArray();
        }

        string key = Hex(Hash(recipe));
        if (ByKey.TryGetValue(key, out Lazy<XlEntry>? hit) && hit.IsValueCreated && hit.Value.Value != null)
        {
            Interlocked.Increment(ref hits);
            return Host.Present(hit.Value);
        }

        object? result = reads ? WithFiles(files, () => body(args)) : body(args);
        if (result == null)
        {
            return ExcelError.ExcelErrorNA;
        }

        int shift = DateShift();
        Lazy<XlEntry> lazy = ByKey.GetOrAdd(key, k => new Lazy<XlEntry>(
            () =>
            {
                Interlocked.Increment(ref builds);
                return Register(new XlEntry(k, result) { Function = fn, Recipe = recipe, Args = (object[])args.Clone(), Shift = shift, Files = files });
            },
            LazyThreadSafetyMode.ExecutionAndPublication));
        return Host.Present(lazy.Value);
    }

    /// <summary>
    /// A value call's result in Excel's form; one holding objects is kept as an entry of its own, so its elements live
    /// while a cell shows it and go with it.
    /// </summary>
    /// <param name="fn">The worksheet function.</param>
    /// <param name="args">Its Excel arguments.</param>
    /// <param name="raw">The call's result.</param>
    /// <returns>What the cell shows.</returns>
    internal static object Present(string fn, object[] args, object? raw)
    {
        var children = new List<XlEntry>();
        object r;
        parent = new XlParent(fn, args, children);
        try
        {
            r = Out(raw);
        }
        finally
        {
            parent = null;
        }

        if (children.Count == 0)
        {
            return r;
        }

        byte[] recipe = Recipe(fn, args);
        string key = Hex(Hash(recipe));
        int shift = DateShift();
        XlEntry en = ByKey.GetOrAdd(key, k => new Lazy<XlEntry>(() => new XlEntry(k, r) { Function = fn, Recipe = recipe, Args = (object[])args.Clone(), Shift = shift, IsValue = true, Children = children })).Value;
        return Host.Present(en);
    }

    // The entry's handle: its type and the key's 16 hex digits, or 32 where another live entry's 16 meet them.
    private static XlEntry Register(XlEntry en)
    {
        lock (ByHandle)
        {
            string tag = en.Value!.GetType().Name.Split('`')[0] + "#";
            string text = tag + en.Key[..16];
            if (ByHandle.TryGetValue(text, out XlEntry? other) && other.Key != en.Key)
            {
                text = tag + en.Key[..32];
            }

            en.Handle = text;
            ByHandle[text] = en;
            return en;
        }
    }

    /// <summary>Releases an entry its last holder let go, with the elements it holds.</summary>
    /// <param name="en">The entry.</param>
    internal static void Release(XlEntry en)
    {
        if (en.Handle != null)
        {
            ByHandle.TryRemove(new KeyValuePair<string, XlEntry>(en.Handle, en));
        }

        if (ByKey.TryGetValue(en.Key, out Lazy<XlEntry>? lazy) && lazy.IsValueCreated && ReferenceEquals(lazy.Value, en))
        {
            ByKey.TryRemove(new KeyValuePair<string, Lazy<XlEntry>>(en.Key, lazy));
        }

        foreach (XlEntry child in en.Children ?? [])
        {
            Release(child);
        }
    }

    // An object inside a returned collection: keyed by its parent call and its place, a row and a slot inside a tuple
    // or a map, never by its identity; it lives with its parent's entry.
    private static string Handle(object r, int position = -1)
    {
        XlParent p = parent ?? throw new InvalidOperationException("an object inside a result with no call presenting it has no key");
        byte[] parentRecipe = Recipe(p.Fn, p.Args);
        string slot = slotPath == null ? position.ToString(CultureInfo.InvariantCulture) : slotPath + "/" + position.ToString(CultureInfo.InvariantCulture);
        string key = Hex(Hash([.. parentRecipe, .. Encoding.UTF8.GetBytes("|" + slot)]));
        bool item = slotPath == null && position >= 0;
        int shift = DateShift();
        XlEntry en = ByKey.GetOrAdd(key, k => new Lazy<XlEntry>(() => Register(new XlEntry(k, r)
        {
            Function = "(element)",
            ParentFn = p.Fn,
            ParentArgs = (object[])p.Args.Clone(),
            ParentKey = Hex(Hash(parentRecipe)),
            Item = item ? position : -1,
            Shift = shift,
        }))).Value;
        p.Children.Add(en);
        return en.Handle!;
    }

    // A call's canonical bytes: its function, the provider, the date system and each argument.
    private static byte[] Recipe(string fn, object[] args)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, true))
        {
            w.Write(fn);
            w.Write(ProviderId);
            w.Write(DateShift());
            foreach (object a in args)
            {
                WriteValue(w, a);
            }
        }

        return ms.ToArray();
    }

    private static void WriteValue(BinaryWriter w, object? a)
    {
        switch (a)
        {
            case double d: w.Write((byte)1); w.Write(BitConverter.DoubleToInt64Bits(d)); break;
            case string s: w.Write((byte)2); w.Write(s); break;
            case bool b: w.Write((byte)3); w.Write(b); break;
            case ExcelMissing: w.Write((byte)4); break;
            case ExcelEmpty: w.Write((byte)5); break;
            case ExcelError e: w.Write((byte)6); w.Write((int)e); break;
            case object[,] m:
                w.Write((byte)7);
                w.Write(m.GetLength(0));
                w.Write(m.GetLength(1));
                foreach (object x in m)
                {
                    WriteValue(w, x);
                }

                break;
            case object[] v:
                w.Write((byte)8);
                w.Write(v.Length);
                foreach (object x in v)
                {
                    WriteValue(w, x);
                }

                break;
            case int i: w.Write((byte)9); w.Write(i); break;
            default: w.Write((byte)99); w.Write(a?.ToString() ?? string.Empty); break;
        }
    }

    private static string Hex(byte[] hash) => Convert.ToHexString(hash);

    /// <summary>The workbook's date system's shift: 1,462 days in a 1904-system workbook, 0 in a 1900 one.</summary>
    /// <returns>The shift.</returns>
    internal static int DateShift() => Host.DateShift();
}
