# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""nanoql for Python: nanoql's types and members over its native library, through the C ABI (docs/bindings.md).

Types keep nanoql's names, members take PEP 8's snake case (``curve.discount(5.0)``), dates are ``datetime.date``,
periods ``nanoql.Period(length, units)`` or a tuple, arrays come back as NumPy's ``float64``. The experimental API is
in ``nanoql.experimental``, which its import opts into.
"""

from ._runtime import (
    ArgumentError,
    ArgumentNullError,
    ArgumentOutOfRangeError,
    BoundEngine,
    Distribution,
    HandleError,
    NanoQLError,
    Object,
    Period,
    abi_version,
    library_path,
    live_handles,
    use_library,
    version,
)
from ._generated import *  # noqa: F401,F403
from ._generated import __all__ as _generated_all

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
    *_generated_all,
]
