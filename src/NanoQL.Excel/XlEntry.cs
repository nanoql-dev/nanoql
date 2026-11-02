// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Excel;

/// <summary>
/// One entry of the store: an object behind its handle, or a value call's result holding objects, with the recipe that
/// made it (XLR3): the function, its Excel arguments, the read files' bytes and the workbook's date system; an object
/// inside a returned collection records its parent call and its place instead (XLD12).
/// </summary>
/// <param name="key">The key: the hash of the call's canonical bytes, as hex.</param>
/// <param name="value">The object, or a value call's Excel result.</param>
internal sealed class XlEntry(string key, object? value)
{
    private int holders;

    /// <summary>Gets the key.</summary>
    public string Key { get; } = key;

    /// <summary>Gets the object, or the value call's Excel result.</summary>
    public object? Value { get; } = value;

    /// <summary>Gets or sets the handle's text; null for a value call's entry.</summary>
    public string? Handle { get; set; }

    /// <summary>Gets or sets whether the entry is a value call's, shown as its value.</summary>
    public bool IsValue { get; set; }

    /// <summary>Gets or sets the worksheet function that made it.</summary>
    public string Function { get; set; } = string.Empty;

    /// <summary>Gets or sets the call's canonical bytes.</summary>
    public byte[]? Recipe { get; set; }

    /// <summary>Gets or sets the call's Excel arguments.</summary>
    public object[]? Args { get; set; }

    /// <summary>Gets or sets the files the call read, by path, their bytes as hashed.</summary>
    public Dictionary<string, byte[]>? Files { get; set; }

    /// <summary>Gets or sets the calling workbook's date shift when the entry was built (1,462 for the 1904 system).</summary>
    public int Shift { get; set; }

    /// <summary>Gets or sets an element's parent call.</summary>
    public string? ParentFn { get; set; }

    /// <summary>Gets or sets an element's parent call's arguments.</summary>
    public object[]? ParentArgs { get; set; }

    /// <summary>Gets or sets an element's parent call's key.</summary>
    public string? ParentKey { get; set; }

    /// <summary>Gets or sets an element's position in its parent's array, or -1 inside a tuple or a map.</summary>
    public int Item { get; set; } = -1;

    /// <summary>Gets or sets the elements a value call's entry holds, released with it.</summary>
    public List<XlEntry>? Children { get; set; }

    /// <summary>Gets what a cell shows: the handle, or the value.</summary>
    public object Shown => IsValue ? Value! : Handle!;

    /// <summary>Gets the RTD topics holding the entry.</summary>
    public int Holders => Volatile.Read(ref holders);

    /// <summary>A topic takes the entry.</summary>
    public void Hold() => Interlocked.Increment(ref holders);

    /// <summary>A topic lets the entry go; the last one releases it from the store.</summary>
    public void Let()
    {
        if (Interlocked.Decrement(ref holders) == 0)
        {
            X.Release(this);
        }
    }
}
