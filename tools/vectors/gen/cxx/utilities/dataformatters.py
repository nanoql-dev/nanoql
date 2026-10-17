# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the output manipulators of ql/utilities/dataformatters.hpp, which no wheel binding or ported QuantLib
test checks (docs/porting-playbook.md §9.4), each output computed by the C++ harness: io::ordinal from 0 to 1000 and beyond;
io::power_of_two for Integer and long long; io::checknull for Real and Integer, over ties, subnormals, signed zeros,
the largest double and the non-finite values; io::sequence; and io::percent, io::rate and io::volatility, with exact
ties at the percentage's sixth decimal. The lists hold every input of the own tests these vectors replaced, and
each null input is QuantLib's null sentinel.
tests/NanoQL.Tests/Vectors/Cxx/Utilities/DataFormattersCxxVectorTests.cs reads it back."""
import math
import os
import sys
from fractions import Fraction

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

ORDINALS = list(range(0, 1001)) + [1011, 1012, 1013, 1111, 10001, 99999, 2147483646]
INTEGERS = [0, 1, 2, 3, 4, 7, 12, 1024, 3 << 20, -1, -4, -12, -2147483648, 2147483646, 1 << 30, 999999, 6 << 10]
# Null<long long>() is Null<Integer>()'s 2147483647, so the null input stands for it; 2147483648 is the first value
# past it.
LONGS = [3 << 40, -(1 << 62), 1 << 62, 5 << 33, -6, 0, 1, 2147483646, 2147483648, -(1 << 63), (1 << 63) - 1, None]
# Ties at the sixth significant digit, subnormals, signed zeros, the smallest normal and the largest double, and
# values on either side of the stream's switch to exponent notation.
REALS = [0.0, -0.0, 1.0, -1.0, 0.1, 0.2, 1 / 3, 2 / 3, 1e-07, 1e-05, 1.5e-05, 0.0001, 0.00012345, 0.000123456789,
         123456.5, 1234565.0, 100000.5, 999999.5, 12345.25, 1234.125, 123456.7, 1234567.0, 1e6, 1e15, 1e16, 1e21,
         1e22, 1e100, 1e300, 5e-324, 2.2250738585072014e-308, 1.7976931348623157e308, 3.141592653589793,
         2.718281828459045, 0.5, 2.5, 99999.95, 0.000099999949, -123.456789, 6.02214076e23]
# The replaced own tests' values REALS doesn't hold.
OWN_REALS = [-2.5, 3.0]
NON_FINITE = [math.nan, math.inf, -math.inf]
SEQUENCES = [[], [1.0], [1.0, 2.5, 3.0], [1e-07, 1 / 3], [123456.5, -0.0, 1e300], [0.1, 0.2, 0.30000000000000004],
             NON_FINITE]
INTEGER_SEQUENCES = [[], [1], [1, 2, 3], [-5, 0, 2147483646]]
PERCENTS = [None, 0.0, -0.0] + NON_FINITE + [0.1, 0.2, 0.0325, -0.015, 1.0, 1e300, 1e307, -1e307,
                                              1.7976931348623157e308, 2e306]


def ties(count):
    """The first count rates whose percentage is an exact tie at its sixth decimal: a percentage j/128, j odd, has a
    seventh decimal 5 and nothing after it, and a rate is kept where rate * 100.0, as percent_holder computes it,
    gives that percentage exactly (some j/128 no double rate reaches)."""
    out, j = [], 1
    while len(out) < count:
        r = j / 128 / 100
        if r * 100.0 == j / 128:
            digits = Fraction(r * 100.0) * 10 ** 7
            assert digits.denominator == 1 and digits.numerator % 10 == 5, r
            out.append(r)
        j += 2
    return out


TIES = ties(40)
RATES = PERCENTS + TIES + [-r for r in TIES[:4]]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))

        def outputs(method, xs):
            return [v.attempt(lambda x=x: H.call(method, x)) for x in xs]

        def case(name, key, xs, **expected):
            return {"name": name, "inputs": {"settings": st, key: v.enc(xs)},
                    "expected": {k: outputs(m, xs) for k, m in expected.items()}}

        return [case("ordinal", "n", ORDINALS, ordinal="io::ordinal"),
                case("powerOfTwo<Integer>", "n", INTEGERS + [None], powerOfTwo="io::power_of_two<Integer>"),
                case("powerOfTwo<long long>", "n", LONGS, powerOfTwo="io::power_of_two<long long>"),
                case("checknull<Real>", "x", REALS + OWN_REALS + NON_FINITE + [None], checkNull="io::checknull<Real>"),
                case("checknull<Integer>", "n", INTEGERS + [42, None], checkNull="io::checknull<Integer>"),
                case("sequence<Real>", "sequences", SEQUENCES, sequence="io::sequence<Real>"),
                case("sequence<Integer>", "sequences", INTEGER_SEQUENCES, sequence="io::sequence<Integer>"),
                case("percent, rate and volatility", "x", RATES, percent="io::percent", rate="io::rate",
                     volatility="io::volatility")]


if __name__ == "__main__":
    v.main(__file__, "ql/utilities/dataformatters.hpp", cases)
