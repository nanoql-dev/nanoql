// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Native;

/// <summary>
/// The handles the C ABI issues: a handle is a 24-bit slot, a 24-bit generation and a 16-bit tag drawn when the library
/// loads, so a released, stale, never-issued or another nanoql library's handle is refused, never followed. A slot whose
/// generation would wrap is retired, never reused; slots are reused first in, first out. Reads take no lock: each slot
/// holds an immutable entry, replaced whole.
/// </summary>
internal static class HandleTable
{
#if NANOQL_TEST_EXPORTS
    /// <summary>The generation's width: 8 bits in the test build, so a churned slot retires after 255 reuses.</summary>
    public const int GenerationBits = 8;
#else
    /// <summary>The generation's width.</summary>
    public const int GenerationBits = 24;
#endif

    private const int SlotBits = 24;
    private const long SlotMask = (1L << SlotBits) - 1;
    private const uint MaxGeneration = (1u << GenerationBits) - 1;
    private const uint Retired = uint.MaxValue;
    private const int ChunkBits = 12;
    private const int ChunkSize = 1 << ChunkBits;

    private static readonly Entry?[]?[] Chunks = new Entry?[]?[1 << (SlotBits - ChunkBits)];
    private static readonly Queue<int> Free = new();
    private static readonly object Gate = new();
    private static int next;
    private static long live;

    /// <summary>Gets this library's tag, never 0, so no handle is NULL.</summary>
    public static ushort Tag { get; } = (ushort)Random.Shared.Next(1, 1 << 16);

    /// <summary>Gets the number of handles issued and not released.</summary>
    public static long Live
    {
        get
        {
            lock (Gate)
            {
                return live;
            }
        }
    }

    /// <summary>Issues a handle to an object; null is the NULL handle.</summary>
    /// <param name="o">The object.</param>
    /// <returns>Its new handle, the caller's.</returns>
    public static nint New(object? o)
    {
        if (o is null)
        {
            return 0;
        }

        int slot;
        uint gen;
        lock (Gate)
        {
            if (Free.Count > 0)
            {
                slot = Free.Dequeue();
            }
            else
            {
                if (next > SlotMask)
                {
                    throw new OutOfMemoryException("nanoql's handle table is full: 16,777,216 handles are live or retired");
                }

                slot = next++;
            }

            Entry?[] chunk = Chunks[slot >> ChunkBits] ??= new Entry?[ChunkSize];
            gen = chunk[slot & (ChunkSize - 1)]?.Generation ?? 0;
            Volatile.Write(ref chunk[slot & (ChunkSize - 1)], new Entry(o, gen));
            live++;
        }

        return (nint)(((long)Tag << 48) | ((long)gen << SlotBits) | (uint)slot);
    }

    /// <summary>The object a handle refers to.</summary>
    /// <param name="h">A handle, not NULL.</param>
    /// <param name="param">The parameter's name, for the refusal.</param>
    /// <returns>The object.</returns>
    /// <exception cref="HandleException">The handle is released, stale, never issued or another library's.</exception>
    public static object Resolve(nint h, string? param)
    {
        (int slot, uint gen) = Split(h, param);
        Entry?[]? chunk = Volatile.Read(ref Chunks[slot >> ChunkBits]);
        Entry? e = chunk is null ? null : Volatile.Read(ref chunk[slot & (ChunkSize - 1)]);
        if (e?.Object is null || e.Generation != gen)
        {
            throw new HandleException(9, $"{param ?? "handle"}: a released or stale handle", param);
        }

        return e.Object;
    }

    /// <summary>Releases a handle; its slot's next handle has the next generation, or the slot retires.</summary>
    /// <param name="h">The handle; NULL releases nothing.</param>
    /// <exception cref="HandleException">The handle is released, stale, never issued or another library's.</exception>
    public static void Release(nint h)
    {
        if (h == 0)
        {
            return;
        }

        (int slot, uint gen) = Split(h, null);
        lock (Gate)
        {
            Entry?[]? chunk = Chunks[slot >> ChunkBits];
            Entry? e = chunk?[slot & (ChunkSize - 1)];
            if (e?.Object is null || e.Generation != gen)
            {
                throw new HandleException(9, "a released or stale handle", null);
            }

            bool retire = gen >= MaxGeneration;
            Volatile.Write(ref chunk![slot & (ChunkSize - 1)], new Entry(null, retire ? Retired : gen + 1));
            if (!retire)
            {
                Free.Enqueue(slot);
            }

            live--;
        }
    }

    private static (int Slot, uint Generation) Split(nint h, string? param)
    {
        long v = h;
        if ((ushort)(v >>> 48) != Tag)
        {
            throw new HandleException(9, $"{param ?? "handle"}: not a handle this library issued", param);
        }

        return ((int)(v & SlotMask), (uint)((v >> SlotBits) & ((1L << SlotBits) - 1)));
    }

    private sealed class Entry(object? o, uint generation)
    {
        public object? Object { get; } = o;

        public uint Generation { get; } = generation;
    }
}
