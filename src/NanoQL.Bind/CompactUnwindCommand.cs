// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Text;

namespace NanoQL.Bind;

/// <summary>
/// <c>nanoql-bind fix-unwind &lt;object&gt;</c>: an Arm64 Mach-O object's compact-unwind entries corrected in place,
/// between Native AOT's compilation and the link (the native project's target on <c>osx-arm64</c>). .NET 10's Native
/// AOT writes a frame-mode entry's register-pair flags for d8 to d15 six bits too high, in bits 14 to 17 instead of 8
/// to 11 (dotnet/runtime#135616), so macOS's unwinder leaves d8 to d15 unrestored after a caught exception, and a
/// caller's floating-point value held there comes back wrong, with no refusal. Each frame-mode entry's misplaced flags
/// move down six bits; an entry with flags in both places is refused, since the encoder that writes it is neither the
/// faulty one nor a fixed one. The faulty encoder ORs the same flags into a frameless entry, over the bits that hold
/// its stack size, where they can't be told apart, so an object with misplaced frame-mode flags and a frameless entry
/// with any of bits 14 to 17 set is refused too. Correcting a corrected object moves nothing, and an object from a
/// fixed Native AOT has nothing to move: its counts say so.
/// </summary>
internal static class CompactUnwindCommand
{
    private const uint MachMagic64 = 0xFEEDFACF;
    private const uint CpuTypeArm64 = 0x0100000C;
    private const uint LoadSegment64 = 0x19;
    private const uint ModeMask = 0x0F000000;
    private const uint ModeFrame = 0x04000000;
    private const uint ModeFrameless = 0x02000000;
    private const uint ModeDwarf = 0x03000000;
    private const uint Misplaced = 0x0003C000;
    private const uint DPairs = 0x00000F00;
    private const int EntrySize = 32;

    /// <summary>Corrects an object file.</summary>
    /// <param name="args">The object's path.</param>
    /// <returns>The exit code.</returns>
    public static int Run(string[] args)
    {
        if (args.Length != 1)
        {
            throw new ArgumentException("fix-unwind takes the object's path: nanoql-bind fix-unwind <object>");
        }

        byte[] data = File.ReadAllBytes(args[0]);
        Counts c = Correct(data);
        if (c.Moved > 0)
        {
            File.WriteAllBytes(args[0], data);
        }

        Console.WriteLine($"{Path.GetFileName(args[0])}: {c.Entries} compact-unwind entries, {c.Frame} frame-mode, "
            + $"{c.Frameless} frameless, {c.Dwarf} DWARF; {c.Moved} moved to their d8 to d15 pair bits, "
            + $"{c.Right} already there");
        return 0;
    }

    /// <summary>Corrects an object's bytes in place.</summary>
    /// <param name="data">The object: a 64-bit Arm64 Mach-O file.</param>
    /// <returns>The entries' counts.</returns>
    /// <exception cref="InvalidDataException">The file isn't a 64-bit Arm64 Mach-O object, or an entry has d8 to d15
    /// flags in both places.</exception>
    internal static Counts Correct(byte[] data)
    {
        if (data.Length < 32 || BinaryPrimitives.ReadUInt32LittleEndian(data) != MachMagic64
            || BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4)) != CpuTypeArm64)
        {
            throw new InvalidDataException("not a 64-bit Arm64 Mach-O object");
        }

        (int Offset, int Size)? section = Section(data);
        if (section is not (int offset, int size))
        {
            return new Counts(0, 0, 0, 0, 0, 0);
        }

        if (size % EntrySize != 0)
        {
            throw new InvalidDataException($"the compact-unwind section's {size} bytes aren't whole entries");
        }

        int frame = 0, frameless = 0, dwarf = 0, moved = 0, right = 0, sized = 0;
        for (int e = offset; e < offset + size; e += EntrySize)
        {
            Span<byte> word = data.AsSpan(e + 12, 4);
            uint encoding = BinaryPrimitives.ReadUInt32LittleEndian(word);
            switch (encoding & ModeMask)
            {
                case ModeFrame:
                    frame++;
                    uint misplaced = encoding & Misplaced;
                    if (misplaced != 0 && (encoding & DPairs) != 0)
                    {
                        throw new InvalidDataException(
                            $"a compact-unwind entry has d8 to d15 flags in both places: 0x{encoding:x8}");
                    }

                    if (misplaced != 0)
                    {
                        BinaryPrimitives.WriteUInt32LittleEndian(word, (encoding & ~Misplaced) | (misplaced >> 6));
                        moved++;
                    }
                    else if ((encoding & DPairs) != 0)
                    {
                        right++;
                    }

                    break;
                case ModeFrameless:
                    frameless++;
                    sized += (encoding & Misplaced) != 0 ? 1 : 0;
                    break;
                case ModeDwarf:
                    dwarf++;
                    break;
            }
        }

        if (moved > 0 && sized > 0)
        {
            throw new InvalidDataException(
                $"{sized} frameless compact-unwind entries may carry misplaced d8 to d15 flags in their stack size");
        }

        return new Counts(size / EntrySize, frame, frameless, dwarf, moved, right);
    }

    // The __LD,__compact_unwind section's file offset and size, from the object's 64-bit segment commands.
    private static (int Offset, int Size)? Section(byte[] data)
    {
        int commands = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(16));
        int at = 32;
        for (int i = 0; i < commands; i++)
        {
            uint command = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at));
            int commandSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at + 4));
            if (command == LoadSegment64)
            {
                int sections = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at + 64));
                for (int s = 0, header = at + 72; s < sections; s++, header += 80)
                {
                    if (Name(data, header) == "__compact_unwind" && Name(data, header + 16) == "__LD")
                    {
                        return ((int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(header + 48)),
                                (int)BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(header + 40)));
                    }
                }
            }

            at += commandSize;
        }

        return null;
    }

    private static string Name(byte[] data, int at) => Encoding.ASCII.GetString(data, at, 16).TrimEnd('\0');

    /// <summary>The entries' counts.</summary>
    /// <param name="Entries">The section's entries.</param>
    /// <param name="Frame">The frame-mode entries.</param>
    /// <param name="Frameless">The frameless entries.</param>
    /// <param name="Dwarf">The entries that defer to DWARF.</param>
    /// <param name="Moved">The frame-mode entries whose flags moved.</param>
    /// <param name="Right">The frame-mode entries whose flags were already in their bits.</param>
    internal sealed record Counts(int Entries, int Frame, int Frameless, int Dwarf, int Moved, int Right);
}
