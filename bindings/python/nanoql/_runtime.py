# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""nanoql's runtime for Python: the native library, objects and their handles, errors, and the C form each kind of
value takes across the C ABI (docs/bindings.md). The generated module names nanoql's types and members; this module
moves their values.

A kind is the surface's kind text (``double``, ``array:double``, ``map:date:double``), with ``@`` and a class's prefix
and name where the value is an object or an enum (``handle@nql.FlatForward``, ``enum@nql.Compounding``), so a user's
class named like one of nanoql's keeps apart; a constructor's result is ``new``, the raw handle its object takes.
"""

from __future__ import annotations

import collections
import ctypes
import datetime
import operator
import os
import sys
import threading
import warnings
from ctypes import POINTER, byref, c_bool, c_char_p, c_double, c_int32, c_int64, c_void_p
from typing import Any, NamedTuple

import numpy as np

__all__ = [
    "ArgumentError",
    "ArgumentNullError",
    "ArgumentOutOfRangeError",
    "BoundEngine",
    "Distribution",
    "HandleError",
    "NanoQLError",
    "Object",
    "Period",
    "abi_version",
    "library_path",
    "live_handles",
    "use_library",
    "version",
]

# ---- errors ----


class NanoQLError(Exception):
    """A refusal from nanoql: its status code (docs/bindings.md, "Errors"), the parameter it names and the .NET
    exception's type."""

    def __init__(self, message: str, code: int = 1, param: str | None = None, type_name: str | None = None) -> None:
        super().__init__(message)
        self.code = code
        self.param = param
        self.type_name = type_name


class ArgumentError(NanoQLError, ValueError):
    """An argument refused (code 2), and the kinds below it."""


class ArgumentNullError(ArgumentError):
    """A required argument missing (code 3), a closed object's handle among them."""


class ArgumentOutOfRangeError(ArgumentError):
    """An argument out of its range (code 4)."""


class HandleError(NanoQLError):
    """A released, stale, foreign or never-issued handle (code 9), or one of the wrong type (code 10); a closed object."""


_ERRORS: dict[int, type[NanoQLError]] = {2: ArgumentError, 3: ArgumentNullError, 4: ArgumentOutOfRangeError, 9: HandleError, 10: HandleError}

# ---- the library ----

_NAMES = {"win32": "nanoql.dll", "darwin": "libnanoql.dylib"}
_lib: ctypes.CDLL | None = None
_path: str | None = None
_lock = threading.Lock()
_abi_expected: int | None = None


def library_path() -> str:
    """The library this package loads: ``NANOQL_LIBRARY`` when set, else the one beside the package."""
    env = os.environ.get("NANOQL_LIBRARY")
    return env if env else os.path.join(os.path.dirname(os.path.abspath(__file__)), "lib", _NAMES.get(sys.platform, "libnanoql.so"))


def use_library(path: str) -> None:
    """Loads nanoql's native library from a path in place of the package's own, before any object is made: objects
    made through another library are foreign to this one, so a call once one exists is refused (RuntimeError)."""
    with _lock:
        if _lib is not None:
            n = c_int64()
            _check(_lib.nql_live_handles(byref(n)))
            if n.value:
                raise RuntimeError(f"use_library: {n.value} objects exist from {_path}; another library can't take them")
        _load(path)


def _library() -> ctypes.CDLL:
    lib = _lib
    if lib is None:
        with _lock:
            if _lib is None:
                _load(library_path())
            lib = _lib
    return lib  # type: ignore[return-value]


def _load(path: str) -> None:
    global _lib, _path
    lib = ctypes.CDLL(path)
    for name, restype, argtypes in (
        ("nql_last_error_code", c_int32, []),
        ("nql_last_error_message", c_char_p, []),
        ("nql_last_error_param", c_char_p, []),
        ("nql_last_error_type", c_char_p, []),
        ("nql_release", c_int32, [c_void_p]),
        ("nql_retain", c_int32, [c_void_p, POINTER(c_void_p)]),
        ("nql_type_name", c_int32, [c_void_p, POINTER(c_void_p)]),
        ("nql_equals", c_int32, [c_void_p, c_void_p, POINTER(c_bool)]),
        ("nql_hash", c_int32, [c_void_p, POINTER(c_int32)]),
        ("nql_to_string", c_int32, [c_void_p, POINTER(c_void_p)]),
        ("nql_live_handles", c_int32, [POINTER(c_int64)]),
        ("nql_free", None, [c_void_p]),
        ("nql_version", c_char_p, []),
        ("nql_abi_version", c_int32, []),
    ):
        f = getattr(lib, name)
        f.restype = restype
        f.argtypes = argtypes
    if _abi_expected is not None and lib.nql_abi_version() != _abi_expected:
        raise ImportError(f"{path} is nanoql's ABI {lib.nql_abi_version()}; this package needs {_abi_expected}")
    _lib = lib
    _path = path
    _functions.clear()


def _expect_abi(abi: int) -> None:
    global _abi_expected
    _abi_expected = abi


def version() -> str:
    """The library's version: ``2.0``."""
    return (_library().nql_version() or b"").decode("utf-8")


def abi_version() -> int:
    """The library's ABI version, the major times 1,000 plus the minor."""
    return int(_library().nql_abi_version())


def live_handles() -> int:
    """The handles the library has issued and nobody released: a diagnostic."""
    n = c_int64()
    _check(_library().nql_live_handles(byref(n)))
    return n.value


def _check(status: int) -> None:
    if status:
        _raise(status)


def _raise(status: int) -> None:
    lib = _library()

    def text(b: bytes | None) -> str | None:
        return None if b is None else b.decode("utf-8", "replace")

    message = text(lib.nql_last_error_message()) or f"nanoql refused the call (status {status})"
    raise _ERRORS.get(status, NanoQLError)(message, status, text(lib.nql_last_error_param()), text(lib.nql_last_error_type()))


def _take_str(p: c_void_p) -> str | None:
    if not p.value:
        return None
    try:
        return ctypes.string_at(p.value).decode("utf-8")
    finally:
        _library().nql_free(p)


# ---- objects ----

_classes: dict[str, type] = {}
_by_cname: dict[str, type] = {}


class Object:
    """An object nanoql holds, through its handle. Released when Python collects it, or at once by ``close()`` or a
    ``with`` block; a closed object refuses any call before it reaches nanoql."""

    __slots__ = ("_h", "__weakref__")
    _type = "System.Object"
    _sealed = False

    def __init__(self, *args: Any, **kwargs: Any) -> None:
        raise TypeError(f"{type(self).__name__} has no constructor that crosses the C ABI")

    def __init_subclass__(cls, **kwargs: Any) -> None:
        super().__init_subclass__(**kwargs)
        t = cls.__dict__.get("_type")
        if t:
            _classes[t] = cls
        # By its prefix and name, as the kinds name it (nql.Calendar): a user's class may take one of nanoql's names.
        k = cls.__dict__.get("_key")
        if k:
            _by_cname[k] = cls

    @property
    def handle(self) -> int | None:
        """The object's handle, or None once closed."""
        return getattr(self, "_h", None)

    @property
    def closed(self) -> bool:
        """Whether the object is closed: its handle released."""
        return getattr(self, "_h", None) is None

    def close(self) -> None:
        """Releases the object's handle; later calls on it raise HandleError. Closing twice does nothing."""
        h = getattr(self, "_h", None)
        self._h = None
        lib = _lib
        if h and lib is not None:
            lib.nql_release(h)

    def __enter__(self) -> Object:
        return self

    def __exit__(self, *exc: object) -> None:
        self.close()

    def __del__(self) -> None:
        try:
            self.close()
        except Exception:  # noqa: BLE001 - the interpreter may be ending
            pass

    def __eq__(self, other: object) -> bool:
        if not isinstance(other, Object):
            return NotImplemented
        r = c_bool()
        _check(_library().nql_equals(_handle(self), _handle(other), byref(r)))
        return bool(r.value)

    def __ne__(self, other: object) -> bool:
        e = self.__eq__(other)
        return e if e is NotImplemented else not e

    def __hash__(self) -> int:
        r = c_int32()
        _check(_library().nql_hash(_handle(self), byref(r)))
        return r.value

    def __str__(self) -> str:
        p = c_void_p()
        _check(_library().nql_to_string(_handle(self), byref(p)))
        return _take_str(p) or ""

    def __repr__(self) -> str:
        return f"<nanoql.{type(self).__name__}: closed>" if self.closed else f"<nanoql.{type(self).__name__}: {self}>"

    @property
    def type_name(self) -> str:
        """The .NET type's full name behind the handle."""
        p = c_void_p()
        _check(_library().nql_type_name(_handle(self), byref(p)))
        return _take_str(p) or ""


class BoundEngine(Object):
    """An engine bound with its inputs (an engine's ``bind`` or ``bind_on``), for the members that take one."""

    __slots__ = ()
    _type = "NanoQL.Native.BoundEngine"
    _key = "nql.BoundEngine"
    _sealed = True


def _handle(o: Any) -> int | None:
    if o is None:
        return None
    if not isinstance(o, Object):
        raise TypeError(f"expected a nanoql object, got {type(o).__name__}")
    h = getattr(o, "_h", None)
    if h is None:
        raise HandleError(f"the {type(o).__name__} is closed", 9, None, None)
    return h


def _wrap(h: int | None, cls: type | None) -> Any:
    """An object for a handle the caller now owns, of the most derived class this module knows."""
    if not h:
        return None
    declared = cls or Object
    actual = declared
    if not getattr(declared, "_sealed", False):
        p = c_void_p()
        if _library().nql_type_name(h, byref(p)) == 0:
            name = _take_str(p)
            found = _classes.get(name or "")
            if found is not None and issubclass(found, declared):
                actual = found
    o = object.__new__(actual)
    o._h = h
    return o


class _ClassBody:
    pass


# The names a class body makes for itself (__module__, __dict__, __doc__, 3.13's __firstlineno__ and the like), which a
# holder's members leave out; its operators and constructors go across (round 6's B6-1, F6-1).
_CLASS_BODY = frozenset(vars(_ClassBody)) | {"__module__", "__qualname__", "__dict__", "__weakref__", "__doc__"}


def _attach(cls: type, holder: type) -> None:
    """Adds a holder class's members to a class: Period's to nanoql.Period, its operators among them, and a stable
    class's experimental members on the opt-in, its constructor among them. A function, so the module binds no name a
    module alias may take (round 5's F5-1)."""
    for name, value in list(vars(holder).items()):
        if name not in _CLASS_BODY:
            setattr(cls, name, value)


def _bases(*bases: type) -> tuple[type, ...]:
    """A class's bases with each one an earlier base already reaches left out, then any interface that would leave no
    consistent order (a type's base and interfaces as nanoql declares them)."""
    kept = [b for b in bases if not any(b is not c and issubclass(c, b) for c in bases)]
    while len(kept) > 1:
        try:
            type("_", tuple(kept), {"__slots__": ()})
            break
        except TypeError:
            kept.pop()
    return tuple(kept) or (Object,)


# ---- values ----

_EPOCH = datetime.date(1899, 12, 30)


class Period(NamedTuple):
    """A period: a length and its units, a ``TimeUnit``. Its operators and its equality are nanoql's: ``Period(1, Years)
    == Period(12, Months)``, and a period of months and one of days compare only where nanoql can decide."""

    length: int
    units: int

    def __eq__(self, other: object) -> bool:
        if isinstance(other, Period):
            return bool(_functions["nql_Period_op_Equality"](self, other))
        return tuple.__eq__(self, other)

    def __ne__(self, other: object) -> bool:
        e = self.__eq__(other)
        return e if e is NotImplemented else not e

    def __hash__(self) -> int:
        # Equal periods hash alike: years as months, weeks as days, an empty period as no days.
        n, u = int(self.length), int(self.units)
        if n == 0:
            return hash((0, 0))
        return hash((n * 12, 2) if u == 3 else (n * 7, 0) if u == 1 else (n, u))


class Distribution(NamedTuple):
    """A loss distribution's normalized buckets: edges, widths, densities, cumulative and excess probabilities, and
    averages."""

    x: np.ndarray
    dx: np.ndarray
    density: np.ndarray
    cumulative: np.ndarray
    excess: np.ndarray
    average: np.ndarray


class _CPeriod(ctypes.Structure):
    _fields_ = [("length", c_int32), ("units", c_int32)]


class _CComplex(ctypes.Structure):
    _fields_ = [("re", c_double), ("im", c_double)]


_time_unit: Any = int
_enums: dict[str, type] = {}


def _serial(d: Any) -> int:
    if d is None:
        return 0
    if isinstance(d, datetime.datetime):
        d = d.date()
    if not isinstance(d, datetime.date):
        raise TypeError(f"expected a datetime.date, got {type(d).__name__}")
    return (d - _EPOCH).days


def _date(serial: int) -> datetime.date | None:
    return None if serial == 0 else _EPOCH + datetime.timedelta(days=serial)


def _period(v: Any) -> _CPeriod:
    if v is None:
        return _CPeriod(0, 0)
    length, units = v
    return _CPeriod(operator.index(length), int(units))


def _out_period(p: _CPeriod) -> Period:
    return Period(p.length, _time_unit(p.units))


def _cstr(v: Any) -> bytes | None:
    if v is None:
        return None
    if isinstance(v, bytes):
        return v
    if not isinstance(v, str):
        raise TypeError(f"expected a str, got {type(v).__name__}")
    return v.encode("utf-8")


# ---- kinds ----


def _split(text: str) -> tuple[str, str | None]:
    # A collection's kind keeps its elements' classes: map:enum@nql.Seniority:double splits no further here.
    if text.startswith(("array:", "span:", "jagged:", "map:", "tuple:")):
        return text, None
    name, _, cls = text.partition("@")
    return name, (cls or None)


def _cls(cname: str | None) -> type | None:
    if cname is None:
        return None
    return _by_cname.get(cname) or _enums.get(cname)


# Elements of collections: the ctypes type, the numpy dtype (or None), a converter in and one out.
_ELEMENTS: dict[str, tuple[Any, Any]] = {
    "double": (c_double, np.float64),
    "double?": (c_double, np.float64),
    "int32": (c_int32, np.int32),
    "int32?": (c_int32, np.int32),
    "enum": (c_int32, np.int32),
    "int64": (c_int64, np.int64),
    "bool": (c_bool, np.bool_),
    "date": (c_int32, np.int32),
    "date?": (c_int32, np.int32),
    "period": (_CPeriod, None),
    "complex": (_CComplex, None),
    "string": (c_char_p, None),
    "handle": (c_void_p, None),
}

_SCALAR_IN = {"double": c_double, "int32": c_int32, "int64": c_int64, "bool": c_bool, "enum": c_int32, "string": c_char_p,
              "date": c_int32, "date?": c_int32, "period": _CPeriod, "complex": _CComplex}
_OPTIONAL = {"double?": c_double, "int32?": c_int32, "int64?": c_int64, "bool?": c_bool, "enum?": c_int32,
             "period?": _CPeriod, "complex?": _CComplex}
_OBJECTS = ("handle", "boxed", "boxed?", "snapshot", "engine")


def _in_value(name: str, v: Any) -> Any:
    """A scalar's C value."""
    if name == "double":
        return float(v)
    if name in ("int32", "int64"):
        return operator.index(v)
    if name == "bool":
        return bool(v)
    if name == "enum":
        return int(v)
    if name == "string":
        return _cstr(v)
    if name in ("date", "date?"):
        return _serial(v)
    if name == "period":
        return _period(v)
    if name == "complex":
        c = complex(v)
        return _CComplex(c.real, c.imag)
    raise TypeError(f"no C value for the kind {name}")


def _optional_value(name: str, v: Any) -> Any:
    base = name[:-1]
    if base == "period":
        return _period(v)
    if base == "complex":
        c = complex(v)
        return _CComplex(c.real, c.imag)
    if base == "double":
        return c_double(float(v))
    if base == "bool":
        return c_bool(bool(v))
    if base == "enum":
        return c_int32(int(v))
    return _OPTIONAL[name](operator.index(v))


def _element_array(e: str, cname: str | None, v: Any, keep: list) -> tuple[Any, Any]:
    """A collection's values as a C array, and its has-value flags for nullables."""
    ct, dtype = _ELEMENTS[e]
    items = list(v)
    n = len(items)
    if e in ("double?", "int32?"):
        has = np.array([x is not None for x in items], dtype=np.bool_)
        vals = np.array([0 if x is None else x for x in items], dtype=dtype)
        keep += [has, vals]
        return vals.ctypes.data_as(POINTER(ct)), has.ctypes.data_as(POINTER(c_bool))
    if e in ("date", "date?"):
        arr = np.array([_serial(x) for x in items], dtype=np.int32)
    elif e == "enum":
        arr = np.array([int(x) for x in items], dtype=np.int32)
    elif dtype is not None:
        arr = np.ascontiguousarray(np.asarray(v if isinstance(v, np.ndarray) else items, dtype=dtype))
    else:
        if e == "period":
            arr = (ct * n)(*[_period(x) for x in items])
        elif e == "complex":
            arr = (ct * n)(*[_CComplex(complex(x).real, complex(x).imag) for x in items])
        elif e == "string":
            arr = (ct * n)(*[_cstr(x) for x in items])
        else:
            arr = (ct * n)(*[_handle(x) for x in items])
        keep.append(arr)
        return ctypes.cast(arr, POINTER(ct)), None
    keep.append(arr)
    return arr.ctypes.data_as(POINTER(ct)), None


class _Kind:
    """One kind's C form: its C parameters in (``cin``), out (``cout``), and the conversions each way."""

    __slots__ = ("_count", "cin", "cls", "cout", "e", "item_names", "items", "name", "text", "v")

    def __init__(self, text: str) -> None:
        self._count = 0
        self.text = text
        self.name, cname = _split(text)
        self.cls = cname
        self.e = self.v = None
        self.items: list[_Kind] = []
        self.item_names: tuple[str, ...] = ()
        n = self.name
        head, _, rest = n.partition(":")
        if head in ("array", "span", "jagged"):
            self.name, self.e = head, rest
        elif head == "map":
            self.name = "map"
            self.e, self.v = rest.split(":", 1) if not rest.startswith("tuple") else (rest, None)
        elif head == "tuple":
            self.name = "tuple"
            names, _, kinds = rest.partition("|")
            self.item_names = tuple(names.split(","))
            self.items = [_Kind(k) for k in _top_split(kinds)]
        self.cin = self._cin()
        self.cout = self._cout()

    # The C parameters in, as CForms.In writes them.
    def _cin(self) -> list[Any]:
        n = self.name
        if n in _SCALAR_IN:
            return [_SCALAR_IN[n]]
        if n in _OPTIONAL:
            return [POINTER(_OPTIONAL[n])]
        if n in _OBJECTS:
            return [c_void_p]
        if n in ("matrix", "matrix&"):
            return [POINTER(c_double), c_int32, c_int32]
        if n in ("vector", "vector&"):
            return [POINTER(c_double), c_int32]
        if n in ("array", "span"):
            e = _split(self.e)[0]  # type: ignore[arg-type]
            ct = _ELEMENTS[e][0]
            return [POINTER(ct)] + ([POINTER(c_bool)] if e in ("double?", "int32?") else []) + [c_int32]
        if n == "jagged":
            return [POINTER(c_double), c_int32, POINTER(c_int32), c_int32]
        if n == "map":
            kt = _ELEMENTS[_split(self.e)[0]][0]  # type: ignore[arg-type]
            ve = _split(self.v)[0]  # type: ignore[arg-type]
            vt = _ELEMENTS[ve][0]
            return [POINTER(kt), POINTER(vt)] + ([POINTER(c_bool)] if ve in ("double?", "int32?") else []) + [c_int32]
        return []

    # The C out-parameters, as CForms.Out writes them.
    def _cout(self) -> list[Any]:
        n = self.name
        if n in ("double", "int32", "enum", "int64", "bool", "date", "date?", "period", "complex"):
            return [_SCALAR_IN[n] if n != "date?" else c_int32]
        if n == "string":
            return [c_void_p]
        if n in _OPTIONAL:
            return [_OPTIONAL[n], c_bool]
        if n in ("handle", "boxed", "boxed?", "snapshot", "new"):
            return [c_void_p]
        if n == "matrix":
            return [POINTER(c_double), c_int32, c_int32]
        if n == "vector":
            return [POINTER(c_double), c_int32]
        if n == "array":
            e = _split(self.e)[0]  # type: ignore[arg-type]
            ct = _ELEMENTS[e][0] if e != "string" else c_void_p
            return [POINTER(ct)] + ([POINTER(c_bool)] if e in ("double?", "int32?") else []) + [c_int32]
        if n == "jagged":
            return [POINTER(c_double), POINTER(c_int32), c_int32]
        if n == "map":
            ke = _split(self.e)[0]  # type: ignore[arg-type]
            ve = _split(self.v)[0]  # type: ignore[arg-type]
            kt = _ELEMENTS[ke][0] if ke != "string" else c_void_p
            vt = _ELEMENTS[ve][0] if ve != "string" else c_void_p
            return [POINTER(kt), POINTER(vt)] + ([POINTER(c_bool)] if ve in ("double?", "int32?") else []) + [c_int32]
        if n == "tuple":
            return [t for k in self.items for t in k.cout]
        if n == "distribution":
            return [POINTER(c_double)] * 6 + [c_int32]
        if n == "draws":
            return [POINTER(c_double), POINTER(c_double), c_void_p]
        if n == "sequences":
            return [POINTER(c_double), POINTER(c_double), c_int32, c_void_p]
        return []

    def put(self, v: Any, args: list, keep: list) -> None:
        """Appends a value's C arguments."""
        n = self.name
        if n in _SCALAR_IN:
            args.append(_in_value(n, v))
        elif n in _OPTIONAL:
            if v is None:
                args.append(None)
            else:
                c = _optional_value(n, v)
                keep.append(c)
                args.append(byref(c))
        elif n in _OBJECTS:
            args.append(_handle(v))
        elif n == "matrix":
            if v is None:
                args += [None, 0, 0]
                return
            a = np.ascontiguousarray(np.asarray(v, dtype=np.float64))
            if a.ndim != 2:
                raise ValueError(f"a matrix has two dimensions; got {a.ndim}")
            keep.append(a)
            args += [a.ctypes.data_as(POINTER(c_double)), a.shape[0], a.shape[1]]
        elif n == "vector":
            if v is None:
                args += [None, 0]
                return
            a = np.ascontiguousarray(np.asarray(v, dtype=np.float64)).ravel()
            keep.append(a)
            args += [a.ctypes.data_as(POINTER(c_double)), a.shape[0]]
        elif n in ("vector&", "matrix&"):
            # A Matrix or Vector the member writes into (R15): a writable, contiguous NumPy array, written in place.
            if v is None:
                args += [None, 0] if n == "vector&" else [None, 0, 0]
                return
            ndim = 1 if n == "vector&" else 2
            if not isinstance(v, np.ndarray) or v.dtype != np.float64 or v.ndim != ndim or not v.flags.c_contiguous or not v.flags.writeable:
                raise TypeError(f"nanoql writes into this argument: a writable, contiguous {ndim}-D numpy array of float64")
            args += [v.ctypes.data_as(POINTER(c_double)), *v.shape]
        elif n == "span":
            e = _split(self.e)[0]  # type: ignore[arg-type]
            dtype = _ELEMENTS[e][1]
            if not isinstance(v, np.ndarray) or v.dtype != dtype or not v.flags.c_contiguous or not v.flags.writeable:
                raise TypeError(f"a span nanoql writes into is a writable, contiguous numpy array of {np.dtype(dtype).name}")
            args += [v.ctypes.data_as(POINTER(_ELEMENTS[e][0])), v.shape[0]]
        elif n == "array":
            e, cname = _split(self.e)  # type: ignore[arg-type]
            if v is None:
                args += [None] + ([None] if e in ("double?", "int32?") else []) + [0]
                return
            p, has = _element_array(e, cname, v, keep)
            args += [p] + ([has] if e in ("double?", "int32?") else []) + [len(v)]
        elif n == "jagged":
            if v is None:
                args += [None, 0, None, 0]
                return
            rows = [np.asarray(r, dtype=np.float64).ravel() for r in v]
            flat = np.ascontiguousarray(np.concatenate(rows) if rows else np.zeros(0))
            lengths = np.array([len(r) for r in rows], dtype=np.int32)
            keep += [flat, lengths]
            args += [flat.ctypes.data_as(POINTER(c_double)), flat.shape[0], lengths.ctypes.data_as(POINTER(c_int32)), len(rows)]
        elif n == "map":
            ke, kc = _split(self.e)  # type: ignore[arg-type]
            ve, vc = _split(self.v)  # type: ignore[arg-type]
            if v is None:
                args += [None, None] + ([None] if ve in ("double?", "int32?") else []) + [0]
                return
            pairs = list(v.items()) if hasattr(v, "items") else list(v)
            kp, _ = _element_array(ke, kc, [p[0] for p in pairs], keep)
            vp, has = _element_array(ve, vc, [p[1] for p in pairs], keep)
            args += [kp, vp] + ([has] if ve in ("double?", "int32?") else []) + [len(pairs)]
        else:
            raise TypeError(f"no C form in for the kind {self.text}")

    def make(self) -> list[Any]:
        """Fresh out-parameters."""
        return [t() for t in self.cout]

    def take(self, outs: list[Any], self_cls: type | None = None) -> Any:
        """The result from its out-parameters; frees what nanoql allocated."""
        return self._take(outs, 0, self_cls)[0]

    def _take(self, outs: list[Any], at: int, self_cls: type | None) -> tuple[Any, int]:
        n = self.name
        free = _library().nql_free
        if n in ("double", "int32", "int64"):
            return outs[at].value, at + 1
        if n == "bool":
            return bool(outs[at].value), at + 1
        if n == "enum":
            return _enum(self.cls, outs[at].value), at + 1
        if n in ("date", "date?"):
            return _date(outs[at].value), at + 1
        if n == "period":
            return _out_period(outs[at]), at + 1
        if n == "complex":
            return complex(outs[at].re, outs[at].im), at + 1
        if n == "string":
            return _take_str(outs[at]), at + 1
        if n in _OPTIONAL:
            if not outs[at + 1].value:
                return None, at + 2
            base = n[:-1]
            o = outs[at]
            v = (_out_period(o) if base == "period" else complex(o.re, o.im) if base == "complex" else _enum(self.cls, o.value)
                 if base == "enum" else bool(o.value) if base == "bool" else o.value)
            return v, at + 2
        if n == "new":
            return outs[at].value, at + 1
        if n in ("handle", "boxed", "boxed?", "snapshot"):
            return _wrap(outs[at].value, _cls(self.cls)), at + 1
        if n == "matrix":
            p, rows, cols = outs[at], outs[at + 1].value, outs[at + 2].value
            a = _copy(p, rows * cols, np.float64).reshape(rows, cols)
            free(p)
            return a, at + 3
        if n == "vector":
            p, count = outs[at], outs[at + 1].value
            a = _copy(p, count, np.float64)
            free(p)
            return a, at + 2
        if n == "array":
            e, cname = _split(self.e)  # type: ignore[arg-type]
            nullable = e in ("double?", "int32?")
            p = outs[at]
            has = outs[at + 1] if nullable else None
            count = outs[at + (2 if nullable else 1)].value
            v = _elements(e, cname, p, has, count, True)
            free(p)
            if has is not None:
                free(has)
            return v, at + (3 if nullable else 2)
        if n == "jagged":
            p, lengths, rows = outs[at], outs[at + 1], outs[at + 2].value
            ls = _copy(lengths, rows, np.int32)
            flat = _copy(p, int(ls.sum()), np.float64)
            free(p)
            free(lengths)
            starts = np.concatenate(([0], np.cumsum(ls))).astype(int)
            return [flat[starts[i]:starts[i + 1]].copy() for i in range(rows)], at + 3
        if n == "map":
            ke, kc = _split(self.e)  # type: ignore[arg-type]
            ve, vc = _split(self.v)  # type: ignore[arg-type]
            nullable = ve in ("double?", "int32?")
            kp, vp = outs[at], outs[at + 1]
            has = outs[at + 2] if nullable else None
            count = outs[at + (3 if nullable else 2)].value
            keys = _elements(ke, kc, kp, None, count, False)
            values = _elements(ve, vc, vp, has, count, False)
            free(kp)
            free(vp)
            if has is not None:
                free(has)
            return dict(zip(keys, values)), at + (4 if nullable else 3)
        if n == "tuple":
            vals = []
            for k in self.items:
                v, at = k._take(outs, at, self_cls)
                vals.append(v)
            return _tuple_type(self.item_names)(*vals), at
        if n == "distribution":
            count = outs[at + 6].value
            parts = [_copy(outs[at + i], count, np.float64) for i in range(6)]
            for i in range(6):
                free(outs[at + i])
            return Distribution(*parts), at + 7
        if n in ("draws", "sequences"):
            seq = n == "sequences"
            values, weights = outs[at], outs[at + 1]
            dim = outs[at + 2].value if seq else 1
            nxt = outs[at + (3 if seq else 2)].value
            count = self._count
            v = _copy(values, count * dim, np.float64)
            w = _copy(weights, count, np.float64)
            free(values)
            free(weights)
            return (v.reshape(count, dim) if seq else v, w, _wrap(nxt, self_cls)), at + (4 if seq else 3)
        raise TypeError(f"no C form out for the kind {self.text}")


def _top_split(s: str) -> list[str]:
    parts, depth, cur = [], 0, []
    for ch in s:
        if ch == "," and depth == 0:
            parts.append("".join(cur))
            cur = []
            continue
        depth += ch == "("
        depth -= ch == ")"
        cur.append(ch)
    if cur:
        parts.append("".join(cur))
    return parts


_tuples: dict[tuple[str, ...], type] = {}


def _tuple_type(names: tuple[str, ...]) -> type:
    t = _tuples.get(names)
    if t is None:
        t = collections.namedtuple("Result", names)  # type: ignore[misc]
        _tuples[names] = t
    return t


def _enum(cname: str | None, value: int) -> Any:
    e = _enums.get(cname or "")
    if e is None:
        return value
    try:
        return e(value)
    except ValueError:
        return value


def _copy(p: Any, count: int, dtype: Any) -> np.ndarray:
    if count == 0 or not p:
        return np.zeros(0, dtype=dtype)
    return np.ctypeslib.as_array(p, shape=(count,)).astype(dtype, copy=True)


def _elements(e: str, cname: str | None, p: Any, has: Any, count: int, as_array: bool) -> Any:
    if count == 0 or not p:
        return np.zeros(0, dtype=np.float64) if (e == "double" and as_array) else []
    if e == "double":
        a = np.ctypeslib.as_array(p, shape=(count,)).copy()
        return a if as_array else a.tolist()
    if e in ("int32", "int64", "bool"):
        a = np.ctypeslib.as_array(p, shape=(count,)).copy()
        return a if as_array else a.tolist()
    if e in ("double?", "int32?"):
        vals = np.ctypeslib.as_array(p, shape=(count,)).tolist()
        flags = [bool(has[i]) for i in range(count)]
        return [v if f else None for v, f in zip(vals, flags)]
    if e == "enum":
        return [_enum(cname, p[i]) for i in range(count)]
    if e in ("date", "date?"):
        return [_date(p[i]) for i in range(count)]
    if e == "period":
        return [_out_period(p[i]) for i in range(count)]
    if e == "complex":
        return [complex(p[i].re, p[i].im) for i in range(count)]
    if e == "string":
        out = []
        free = _library().nql_free
        for i in range(count):
            s = p[i]
            out.append(None if not s else ctypes.string_at(s).decode("utf-8"))
            if s:
                free(s)
        return out
    return [_wrap(p[i], _cls(cname)) for i in range(count)]


# ---- functions ----


class _Function:
    """A C function: its prototype set from its kinds, called with Python values."""

    __slots__ = ("fn", "ins", "name", "out")

    def __init__(self, name: str, ins: tuple[str, ...], out: str | None) -> None:
        self.name = name
        self.ins = [_kind(k) for k in ins]
        self.out = _kind(out) if out else None
        fn = getattr(_library(), name)
        fn.restype = c_int32
        fn.argtypes = [t for k in self.ins for t in k.cin] + ([POINTER(t) for t in self.out.cout] if self.out else [])
        self.fn = fn

    def __call__(self, *args: Any) -> Any:
        cargs: list[Any] = []
        keep: list[Any] = []
        for k, a in zip(self.ins, args):
            k.put(a, cargs, keep)
        out = self.out
        if out is None:
            status = self.fn(*cargs)
            if status:
                _raise(status)
            return None
        outs = out.make()
        status = self.fn(*cargs, *[byref(o) for o in outs])
        if status:
            _raise(status)
        if out.name in ("draws", "sequences"):
            k = _Kind(out.text)
            k._count = args[1]
            return k.take(outs, type(args[0]))
        return out.take(outs)


_kinds: dict[str, _Kind] = {}


def _kind(text: str) -> _Kind:
    k = _kinds.get(text)
    if k is None:
        k = _kinds[text] = _Kind(text)
    return k


class _Functions(dict):
    """The C functions by name, each made on its first call from the generated table."""

    table: dict[str, tuple[tuple[str, ...], str | None]] = {}

    def __missing__(self, name: str) -> _Function:
        ins, out = self.table[name]
        f = self[name] = _Function(name, ins, out)
        return f


_functions = _Functions()


def _register(table: dict[str, tuple[tuple[str, ...], str | None]]) -> None:
    _Functions.table.update(table)


# ---- overloads ----


def _score(text: str, v: Any) -> int | None:
    """How well a value matches a kind: 0 its own type, 1 a conversion, None no match; an object nearer its class
    scores lower (by its distance in the class's order)."""
    head, _, rest = text.partition(":")
    name, cname = (head, None) if head in ("array", "span", "jagged", "map") else _split(text)
    if name in ("double", "double?"):
        if v is None:
            return 0 if name.endswith("?") else None
        if isinstance(v, bool):
            return None
        return 0 if isinstance(v, (float, np.floating)) else 1 if isinstance(v, (int, np.integer)) else None
    if name in ("int32", "int64", "int32?", "int64?"):
        if v is None:
            return 0 if name.endswith("?") else None
        if isinstance(v, bool):
            return 1
        return 0 if isinstance(v, (int, np.integer)) else None
    if name in ("bool", "bool?"):
        if v is None:
            return 0 if name.endswith("?") else None
        return 0 if isinstance(v, (bool, np.bool_)) else None
    if name in ("enum", "enum?"):
        if v is None:
            return 0 if name.endswith("?") else None
        e = _enums.get(cname or "")
        if e is not None and isinstance(v, e):
            return 0
        return 2 if isinstance(v, (int, np.integer)) and not isinstance(v, bool) else None
    if name == "string":
        return 0 if v is None or isinstance(v, str) else None
    if name in ("date", "date?"):
        if v is None:
            return 0 if name == "date?" else 1
        return 0 if isinstance(v, datetime.date) else None
    if name in ("period", "period?"):
        if v is None:
            return 0 if name.endswith("?") else None
        if isinstance(v, Period):
            return 0
        return 1 if isinstance(v, tuple) and len(v) == 2 else None
    if name in ("complex", "complex?"):
        if v is None:
            return 0 if name.endswith("?") else None
        return 0 if isinstance(v, complex) else 1 if isinstance(v, (float, int)) and not isinstance(v, bool) else None
    if name in _OBJECTS:
        if v is None:
            return 1
        c = _cls(cname) or Object
        if not isinstance(v, c):
            return None
        return 10 * type(v).__mro__.index(c) if c in type(v).__mro__ else 0
    if name == "matrix":
        if v is None:
            return 1
        if isinstance(v, np.ndarray):
            return 0 if v.ndim == 2 else None
        return 1 if isinstance(v, (list, tuple)) and (not v or isinstance(v[0], (list, tuple, np.ndarray))) else None
    if name == "vector":
        if v is None:
            return 1
        if isinstance(v, np.ndarray):
            return 0 if v.ndim == 1 else None
        return 1 if isinstance(v, (list, tuple)) else None
    if name in ("array", "jagged"):
        if v is None:
            return 1
        if isinstance(v, (str, bytes)) or hasattr(v, "keys") or not hasattr(v, "__len__") or not hasattr(v, "__getitem__"):
            return None
        if len(v) == 0:
            return 1
        e = rest
        first = v[0]
        return 0 if (name == "jagged" and hasattr(first, "__len__")) or (name == "array" and _score(e, first) == 0) else (
            1 if name == "array" and _score(e, first) is not None else None)
    if name == "span":
        return 0 if isinstance(v, np.ndarray) and v.ndim == 1 else None
    if name in ("vector&", "matrix&"):
        if v is None:
            return 1
        return 0 if isinstance(v, np.ndarray) and v.ndim == (1 if name == "vector&" else 2) else None
    if name == "map":
        if v is None:
            return 1
        if hasattr(v, "items"):
            return 0
        return 1 if isinstance(v, (list, tuple)) and all(isinstance(x, tuple) and len(x) == 2 for x in v) else None
    return None


def _dispatch(forms: tuple, args: tuple, kwargs: dict) -> Any:
    """Calls the overload the arguments fit best."""
    call, values, _ = _select(forms, args, kwargs)
    return call(*values)


def _select(forms: tuple, args: tuple, kwargs: dict) -> tuple[Any, list[Any], str]:
    """The overload the arguments fit best, its arguments and its C function: per form its function, parameter names,
    kinds, defaults, the argument counts it's offered at (a count another form takes exactly isn't offered) and its C
    function's name."""
    best = None
    best_score = None
    for call, names, kinds, defaults, offered, cname in forms:
        if len(args) > len(names):
            continue
        values = list(args)
        ok = True
        for i in range(len(args), len(names)):
            if names[i] in kwargs:
                values.append(kwargs[names[i]])
            elif defaults[i] is not _REQUIRED:
                values.append(defaults[i])
            else:
                ok = False
                break
        if not ok or any(k not in names for k in kwargs):
            continue
        given = len(args) + sum(1 for k in kwargs if k in names)
        if given not in offered and not kwargs:
            continue
        total = 0
        for kind, v in zip(kinds, values):
            s = _score(kind, v)
            if s is None:
                ok = False
                break
            total += s
        if ok and (best_score is None or total < best_score):
            best, best_score = (call, values, cname), total
    if best is None:
        raise TypeError("no overload takes these arguments")
    return best


def _deprecated(message: str) -> None:
    warnings.warn(message, DeprecationWarning, stacklevel=3)


class _Required:
    def __repr__(self) -> str:
        return "<required>"


_REQUIRED = _Required()
