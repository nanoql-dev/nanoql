// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;

namespace NanoQL.Native;

/// <summary>
/// The C ABI's fixed functions (docs/bindings.md, "Fixed functions"): the last error, handles' release, second owners,
/// type names, equality, hashes and text, the live-handle count, memory, and versions.
/// </summary>
internal static unsafe class FixedExports
{
    private static readonly byte* VersionText = Rt.NewStr(GeneratedVersion.Text);

    [UnmanagedCallersOnly(EntryPoint = "nql_last_error_code")]
    private static int LastErrorCode() => Rt.LastCode;

    [UnmanagedCallersOnly(EntryPoint = "nql_last_error_message")]
    private static byte* LastErrorMessage() => Rt.LastMessage;

    [UnmanagedCallersOnly(EntryPoint = "nql_last_error_param")]
    private static byte* LastErrorParam() => Rt.LastParam;

    [UnmanagedCallersOnly(EntryPoint = "nql_last_error_type")]
    private static byte* LastErrorType() => Rt.LastType;

    [UnmanagedCallersOnly(EntryPoint = "nql_release")]
    private static int Release(nint h)
    {
        try
        {
            HandleTable.Release(h);
            return 0;
        }
        catch (Exception e)
        {
            return Rt.Fail(e);
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "nql_retain")]
    private static int Retain(nint h, nint* result)
    {
        try
        {
            *result = h == 0 ? 0 : HandleTable.New(HandleTable.Resolve(h, "h"));
            return 0;
        }
        catch (Exception e)
        {
            return Rt.Fail(e);
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "nql_type_name")]
    private static int TypeName(nint h, byte** result)
    {
        try
        {
            *result = Rt.NewStr(Rt.TypeName(Rt.Required<object>(h, "h")));
            return 0;
        }
        catch (Exception e)
        {
            return Rt.Fail(e);
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "nql_equals")]
    private static int EqualsHandles(nint a, nint b, byte* result)
    {
        try
        {
            *result = Equals(Rt.Get<object>(a, "a"), Rt.Get<object>(b, "b")) ? (byte)1 : (byte)0;
            return 0;
        }
        catch (Exception e)
        {
            return Rt.Fail(e);
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "nql_hash")]
    private static int Hash(nint h, int* result)
    {
        try
        {
            *result = Rt.Required<object>(h, "h").GetHashCode();
            return 0;
        }
        catch (Exception e)
        {
            return Rt.Fail(e);
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "nql_to_string")]
    private static int ToText(nint h, byte** result)
    {
        try
        {
            *result = Rt.NewStr(Rt.Required<object>(h, "h").ToString());
            return 0;
        }
        catch (Exception e)
        {
            return Rt.Fail(e);
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "nql_live_handles")]
    private static int LiveHandles(long* result)
    {
        *result = HandleTable.Live;
        return 0;
    }

    [UnmanagedCallersOnly(EntryPoint = "nql_free")]
    private static void Free(void* p) => NativeMemory.Free(p);

    [UnmanagedCallersOnly(EntryPoint = "nql_version")]
    private static byte* Version() => VersionText;

    [UnmanagedCallersOnly(EntryPoint = "nql_abi_version")]
    private static int AbiVersion() => GeneratedVersion.Abi;

#if NANOQL_TEST_EXPORTS
    // The test build's own: this library's tag and its generation's width, for the conformance suites.
    [UnmanagedCallersOnly(EntryPoint = "nqlt_handle_tag")]
    private static int HandleTag() => HandleTable.Tag;

    [UnmanagedCallersOnly(EntryPoint = "nqlt_generation_bits")]
    private static int GenerationBits() => HandleTable.GenerationBits;
#endif
}
