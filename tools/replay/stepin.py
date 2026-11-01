# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Stepping from a C program into nanoql's C# source (docs/bindings.md, "Debugging"): runs the program as a debuggee,
loads the native library's PDB when the library loads, sets a breakpoint on every symbol matching a mask, and at the
first hit walks the stack with DbgHelp, printing each frame's function and its source file and line, the line records a
debugger stepping into nanoql needs. Windows only; DbgHelp from the Windows SDK's debuggers.

Usage: python -I tools/replay/stepin.py <exe> <native library's file name> <symbol mask>

It passes (exit 0) when the stopped frame is a nanoql .cs file's line and a frame above it is a generated export's.
"""
import ctypes
import ctypes.wintypes as wt
import os
import re
import sys

sys.stdout.reconfigure(encoding="utf-8")
k32 = ctypes.WinDLL("kernel32", use_last_error=True)
dbg = ctypes.WinDLL(r"C:\Program Files (x86)\Windows Kits\10\Debuggers\x64\dbghelp.dll")
exe, dllname, mask = os.path.abspath(sys.argv[1]), sys.argv[2].lower(), sys.argv[3]
DEBUG_ONLY_THIS_PROCESS = 0x2
EXCEPTION_DEBUG_EVENT, CREATE_PROCESS_DEBUG_EVENT, EXIT_PROCESS_DEBUG_EVENT, LOAD_DLL_DEBUG_EVENT = 1, 3, 5, 6
DBG_CONTINUE, DBG_EXCEPTION_NOT_HANDLED = 0x10002, 0x80010001
EXCEPTION_BREAKPOINT = 0x80000003


class STARTUPINFOW(ctypes.Structure):
    _fields_ = [("cb", wt.DWORD), ("r", ctypes.c_byte * 100)]


class PROCESS_INFORMATION(ctypes.Structure):
    _fields_ = [("hProcess", wt.HANDLE), ("hThread", wt.HANDLE), ("pid", wt.DWORD), ("tid", wt.DWORD)]


class DEBUG_EVENT(ctypes.Structure):
    _fields_ = [("code", wt.DWORD), ("pid", wt.DWORD), ("tid", wt.DWORD), ("pad", wt.DWORD), ("u", ctypes.c_byte * 160)]


si = STARTUPINFOW()
si.cb = ctypes.sizeof(si)
pi = PROCESS_INFORMATION()
assert k32.CreateProcessW(exe, None, None, None, False, DEBUG_ONLY_THIS_PROCESS, None, os.path.dirname(exe), ctypes.byref(si), ctypes.byref(pi)), ctypes.get_last_error()
dbg.SymSetOptions(0x10 | 0x2)
assert dbg.SymInitializeW(pi.hProcess, os.path.dirname(exe), False)
dbg.SymLoadModuleExW.restype = ctypes.c_uint64
dbg.SymFunctionTableAccess64.restype = ctypes.c_void_p
dbg.SymFunctionTableAccess64.argtypes = [wt.HANDLE, ctypes.c_uint64]
dbg.SymGetModuleBase64.restype = ctypes.c_uint64
dbg.SymGetModuleBase64.argtypes = [wt.HANDLE, ctypes.c_uint64]


class SYMBOL_INFOW(ctypes.Structure):
    _fields_ = [("SizeOfStruct", wt.ULONG), ("TypeIndex", wt.ULONG), ("Reserved", ctypes.c_uint64 * 2), ("Index", wt.ULONG),
                ("Size", wt.ULONG), ("ModBase", ctypes.c_uint64), ("Flags", wt.ULONG), ("Value", ctypes.c_uint64),
                ("Address", ctypes.c_uint64), ("Register", wt.ULONG), ("Scope", wt.ULONG), ("Tag", wt.ULONG),
                ("NameLen", wt.ULONG), ("MaxNameLen", wt.ULONG), ("Name", wt.WCHAR * 1000)]


class IMAGEHLP_LINEW64(ctypes.Structure):
    _fields_ = [("SizeOfStruct", wt.DWORD), ("Key", ctypes.c_void_p), ("LineNumber", wt.DWORD), ("FileName", wt.LPWSTR), ("Address", ctypes.c_uint64)]


def where(addr):
    si_ = SYMBOL_INFOW()
    si_.SizeOfStruct = ctypes.sizeof(SYMBOL_INFOW) - ctypes.sizeof(wt.WCHAR * 1000) + ctypes.sizeof(wt.WCHAR)
    si_.MaxNameLen = 1000
    disp = ctypes.c_uint64()
    name = si_.Name if dbg.SymFromAddrW(pi.hProcess, ctypes.c_uint64(addr), ctypes.byref(disp), ctypes.byref(si_)) else "?"
    ln = IMAGEHLP_LINEW64()
    ln.SizeOfStruct = ctypes.sizeof(ln)
    d2 = wt.DWORD()
    line = f"{ln.FileName}:{ln.LineNumber}" if dbg.SymGetLineFromAddrW64(pi.hProcess, ctypes.c_uint64(addr), ctypes.byref(d2), ctypes.byref(ln)) else "(no source line)"
    return name, line


# x64 CONTEXT: 1232 bytes, 16-byte aligned; ContextFlags at 0x30, Rip at 0xF8, Rsp at 0x98, Rbp at 0xA0.
CTX_SIZE = 1232
ctxbuf = (ctypes.c_byte * (CTX_SIZE + 16))()
ctxaddr = (ctypes.addressof(ctxbuf) + 15) & ~15
CONTEXT_FULL = 0x10000B


def get_ctx(th):
    ctypes.c_uint32.from_address(ctxaddr + 0x30).value = CONTEXT_FULL
    assert k32.GetThreadContext(th, ctypes.c_void_p(ctxaddr)), ctypes.get_last_error()


def reg(off):
    return ctypes.c_uint64.from_address(ctxaddr + off)


class ADDRESS64(ctypes.Structure):
    _fields_ = [("Offset", ctypes.c_uint64), ("Segment", wt.WORD), ("Mode", wt.DWORD)]


class STACKFRAME64(ctypes.Structure):
    _fields_ = [("AddrPC", ADDRESS64), ("AddrReturn", ADDRESS64), ("AddrFrame", ADDRESS64), ("AddrStack", ADDRESS64),
                ("AddrBStore", ADDRESS64), ("FuncTableEntry", ctypes.c_void_p), ("Params", ctypes.c_uint64 * 4),
                ("Far", wt.BOOL), ("Virtual", wt.BOOL), ("Reserved", ctypes.c_uint64 * 3), ("KdHelp", ctypes.c_byte * 120)]


breakpoints = {}
frames = []
CB = ctypes.WINFUNCTYPE(wt.BOOL, ctypes.POINTER(SYMBOL_INFOW), wt.ULONG, ctypes.c_void_p)
ev = DEBUG_EVENT()
hits = 0
while k32.WaitForDebugEvent(ctypes.byref(ev), 30000):
    status = DBG_CONTINUE
    if ev.code == LOAD_DLL_DEBUG_EVENT:
        hfile = ctypes.c_void_p.from_buffer(ev.u, 0).value
        base = ctypes.c_uint64.from_buffer(ev.u, 8).value
        buf = ctypes.create_unicode_buffer(1024)
        k32.GetFinalPathNameByHandleW(wt.HANDLE(hfile), buf, 1024, 0)
        if buf.value.lower().endswith(dllname):
            mb = dbg.SymLoadModuleExW(pi.hProcess, None, buf.value[4:] if buf.value.startswith("\\\\?\\") else buf.value, None, ctypes.c_uint64(base), 0, None, 0)
            found = []

            def cb(s, size, c):
                n = s.contents.Name
                if not n.startswith(("unwind", "_readonlydata")):
                    found.append((n, s.contents.Address))
                return True

            dbg.SymEnumSymbolsW(pi.hProcess, ctypes.c_uint64(mb), mask, CB(cb), None)
            for n, a in found:
                orig = ctypes.c_ubyte()
                k32.ReadProcessMemory(pi.hProcess, ctypes.c_void_p(a), ctypes.byref(orig), 1, None)
                k32.WriteProcessMemory(pi.hProcess, ctypes.c_void_p(a), ctypes.byref(ctypes.c_ubyte(0xCC)), 1, None)
                breakpoints[a] = (n, orig.value)
            k32.FlushInstructionCache(pi.hProcess, None, 0)
            print(f"loaded {buf.value.split(chr(92))[-1]}: {len(found)} breakpoints on {mask}")
    elif ev.code == EXCEPTION_DEBUG_EVENT:
        code = ctypes.c_uint32.from_buffer(ev.u, 0).value
        addr = ctypes.c_uint64.from_buffer(ev.u, 16).value
        if code == EXCEPTION_BREAKPOINT and addr in breakpoints:
            n, orig = breakpoints.pop(addr)
            k32.WriteProcessMemory(pi.hProcess, ctypes.c_void_p(addr), ctypes.byref(ctypes.c_ubyte(orig)), 1, None)
            k32.FlushInstructionCache(pi.hProcess, None, 0)
            th = k32.OpenThread(0x1FFFFF, False, ev.tid)
            get_ctx(th)
            reg(0xF8).value = addr
            k32.SetThreadContext(th, ctypes.c_void_p(ctxaddr))
            if hits == 0:
                print("stopped in nanoql; the stack:")
                frame = STACKFRAME64()
                frame.AddrPC.Offset, frame.AddrPC.Mode = addr, 3
                frame.AddrStack.Offset, frame.AddrStack.Mode = reg(0x98).value, 3
                frame.AddrFrame.Offset, frame.AddrFrame.Mode = reg(0xA0).value, 3
                for i in range(12):
                    if not dbg.StackWalk64(0x8664, pi.hProcess, th, ctypes.byref(frame), ctypes.c_void_p(ctxaddr), None,
                                           dbg.SymFunctionTableAccess64, dbg.SymGetModuleBase64, None):
                        break
                    fn, line = where(frame.AddrPC.Offset)
                    frames.append(line)
                    print(f"  #{i} {fn[:80]}  {line}")
            hits += 1
            k32.CloseHandle(th)
        elif code != EXCEPTION_BREAKPOINT:
            status = DBG_EXCEPTION_NOT_HANDLED
    elif ev.code == EXIT_PROCESS_DEBUG_EVENT:
        print(f"debuggee exited; breakpoint hits {hits}")
        break
    k32.ContinueDebugEvent(ev.pid, ev.tid, status)

# The stopped frame a nanoql .cs line, a frame above it a generated export's.
stopped = bool(frames) and re.search(r"\.cs:\d+$", frames[0]) is not None and not re.search(r"\.g\.cs:\d+$", frames[0])
above = any(re.search(r"Exports\d+\.g\.cs:\d+$", f) for f in frames[1:])
print(f"step-in: stopped in a nanoql source line {stopped}; an export's frame above it {above}")
sys.exit(0 if stopped and above else 1)
