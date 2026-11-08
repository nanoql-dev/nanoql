// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Text;

namespace NanoQL.Bind.Tests;

/// <summary>
/// <c>nanoql-bind fix-unwind</c> (docs/bindings.md, "The tool"): on an object built here, one 64-bit segment holding a
/// <c>__LD,__compact_unwind</c> section, a frame-mode entry's d8 to d15 pair flags move from bits 14 to 17 down to 8
/// to 11, other modes' entries and right flags stay, a corrected object has nothing to move, an entry with flags in
/// both places, a frameless entry whose stack size could hide them beside a misplaced one, and a file that isn't an
/// Arm64 Mach-O object are refused, and an object without the section is left as it is.
/// </summary>
public class CompactUnwindTests
{
    private const uint Frame = 0x04000000;

    [Fact]
    public void AFrameEntrysMisplacedPairFlagsMoveDownSixBits()
    {
        byte[] data = MachObject(Frame | 0x4001, Frame | 0x3C003, 0x02001000, 0x03000000, Frame | 0x100);

        CompactUnwindCommand.Counts c = CompactUnwindCommand.Correct(data);

        Assert.Equal(new CompactUnwindCommand.Counts(5, 3, 1, 1, 2, 1), c);
        Assert.Equal([Frame | 0x101, Frame | 0xF03, 0x02001000u, 0x03000000u, Frame | 0x100], Encodings(data));
    }

    [Fact]
    public void ACorrectedObjectHasNothingToMove()
    {
        byte[] data = MachObject(Frame | 0x4001, Frame | 0x10000);
        CompactUnwindCommand.Correct(data);
        byte[] once = (byte[])data.Clone();

        CompactUnwindCommand.Counts c = CompactUnwindCommand.Correct(data);

        Assert.Equal(new CompactUnwindCommand.Counts(2, 2, 0, 0, 0, 2), c);
        Assert.Equal(once, data);
    }

    [Fact]
    public void AnEntryWithPairFlagsInBothPlacesIsRefused()
    {
        byte[] data = MachObject(Frame | 0x4100);

        Assert.Throws<InvalidDataException>(() => CompactUnwindCommand.Correct(data));
    }

    [Fact]
    public void AFramelessEntryWhoseSizeCouldHideTheFlagsIsRefusedBesideAMisplacedOne()
    {
        Assert.Throws<InvalidDataException>(() => CompactUnwindCommand.Correct(MachObject(Frame | 0x4001, 0x02004000)));
        byte[] right = MachObject(Frame | 0x101, 0x02004000);
        Assert.Equal(new CompactUnwindCommand.Counts(2, 1, 1, 0, 0, 1), CompactUnwindCommand.Correct(right));
    }

    [Fact]
    public void OnlyAnArm64MachObjectIsRead()
    {
        byte[] data = MachObject(Frame | 0x4001);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 0x01000007);

        Assert.Throws<InvalidDataException>(() => CompactUnwindCommand.Correct(data));
    }

    [Fact]
    public void AnObjectWithoutTheSectionIsLeftAsItIs()
    {
        byte[] data = MachObject(Frame | 0x4001);
        Encoding.ASCII.GetBytes("__text\0\0\0\0\0\0\0\0\0\0").CopyTo(data, 32 + 72);
        byte[] before = (byte[])data.Clone();

        Assert.Equal(new CompactUnwindCommand.Counts(0, 0, 0, 0, 0, 0), CompactUnwindCommand.Correct(data));
        Assert.Equal(before, data);
    }

    [Fact]
    public void TheCommandCorrectsTheFileInPlace()
    {
        string path = Path.Combine(Path.GetTempPath(), "nanoql-bind-fix-unwind-" + Guid.NewGuid().ToString("N") + ".o");
        File.WriteAllBytes(path, MachObject(Frame | 0x8001));
        try
        {
            Assert.Equal(0, Program.Run(["fix-unwind", path], Path.GetTempPath(), (_, _) => 0));
            Assert.Equal([Frame | 0x201], Encodings(File.ReadAllBytes(path)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // A 64-bit Arm64 Mach-O object: the header, one segment command with one section, __LD,__compact_unwind, and its
    // entries, each 32 bytes with its encoding at offset 12.
    private static byte[] MachObject(params uint[] encodings)
    {
        const int header = 32, segment = 72 + 80;
        int entries = encodings.Length * 32;
        byte[] data = new byte[header + segment + entries];
        Span<byte> d = data;
        BinaryPrimitives.WriteUInt32LittleEndian(d, 0xFEEDFACF);
        BinaryPrimitives.WriteUInt32LittleEndian(d[4..], 0x0100000C);
        BinaryPrimitives.WriteUInt32LittleEndian(d[12..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(d[16..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(d[20..], segment);
        BinaryPrimitives.WriteUInt32LittleEndian(d[header..], 0x19);
        BinaryPrimitives.WriteUInt32LittleEndian(d[(header + 4)..], segment);
        BinaryPrimitives.WriteUInt32LittleEndian(d[(header + 64)..], 1);
        int section = header + 72;
        Encoding.ASCII.GetBytes("__compact_unwind").CopyTo(data, section);
        Encoding.ASCII.GetBytes("__LD").CopyTo(data, section + 16);
        BinaryPrimitives.WriteUInt64LittleEndian(d[(section + 40)..], (ulong)entries);
        BinaryPrimitives.WriteUInt32LittleEndian(d[(section + 48)..], header + segment);
        for (int i = 0; i < encodings.Length; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(d[(header + segment + (i * 32) + 12)..], encodings[i]);
        }

        return data;
    }

    private static uint[] Encodings(byte[] data)
    {
        const int first = 32 + 72 + 80;
        return Enumerable.Range(0, (data.Length - first) / 32)
            .Select(i => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(first + (i * 32) + 12))).ToArray();
    }
}
