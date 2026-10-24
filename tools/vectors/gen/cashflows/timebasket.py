# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's time basket (ql/cashflows/timebasket.hpp), through the wheel's TimeBasket: baskets of dated
values, a repeated date keeping its last value, rebinned on sorted, unsorted and repeated buckets, with values before
the first bucket, after the last, on a bucket and between two (split by days).

Each "basket" case gives size, the items' dates and values in date order (itemDates, itemValues), hasDate and the value
at the probe dates (an absent date's value is 0, which QuantLib's indexer inserts, so the values are read last), and for
each bucket set the rebinned basket's dates and values (rebinDates, rebinValues); dates and values of other lengths, and
an empty bucket set, are QuantLib's refusals (throws). The basket's sum and difference, which the wheel doesn't bind,
are the C++ harness's (cxx/cashflows/timebasket). Read back by
tests/NanoQL.Tests/Vectors/Coupons/TimeBasketVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

TODAY = ql.Date(15, ql.January, 2025)
D = ql.Date

# (label, dates, values, probes, bucket sets)
BASKETS = [
    ("three flows", [D(1, 2, 2025), D(15, 3, 2025), D(1, 9, 2025)], [100.0, 50.0, -20.0],
     [D(1, 2, 2025), D(2, 2, 2025), D(1, 9, 2025)],
     [[D(1, 1, 2025), D(1, 6, 2025), D(1, 12, 2025)], [D(1, 6, 2025), D(1, 1, 2025)], [D(15, 3, 2025)],
      [D(1, 3, 2025), D(1, 3, 2025), D(1, 10, 2025)]]),
    ("a repeated date keeps its last value", [D(1, 1, 2025), D(15, 3, 2025), D(1, 1, 2025)], [100.0, 50.0, 70.0],
     [D(1, 1, 2025), D(15, 3, 2025)], [[D(1, 6, 2025), D(1, 2, 2025)]]),
    ("values before, on and after the buckets", [D(10, 1, 2025), D(1, 4, 2025), D(20, 5, 2025), D(31, 12, 2026)],
     [1e6, 2.5e5, 3.3e5, -4e4], [D(10, 1, 2025), D(31, 12, 2026), D(1, 1, 2030)],
     [[D(1, 4, 2025), D(1, 7, 2025)], [D(1, 2, 2025), D(1, 4, 2025), D(29, 2, 2028)], [D(1, 1, 2020)]]),
    ("one value", [D(29, 2, 2028)], [12.5], [D(29, 2, 2028), D(28, 2, 2028)],
     [[D(1, 1, 2028), D(1, 1, 2029)], [D(29, 2, 2028)], []]),
    ("empty", [], [], [D(1, 1, 2025)], [[D(1, 1, 2025), D(1, 1, 2026)]]),
    ("dates and values of other lengths", [D(1, 1, 2025), D(1, 2, 2025)], [1.0], [], []),
]


def items(basket):
    pairs = list(basket.items())
    return v.enc([d for d, _ in pairs]), [x for _, x in pairs]


def cases():
    out = []
    for k, (label, dates, values, probes, buckets) in enumerate(BASKETS):
        with v.case_settings(TODAY) as st:
            inputs = {"settings": st, "dates": v.enc(dates), "values": values, "probes": v.enc(probes),
                      "buckets": [v.enc(b) for b in buckets]}
            try:
                basket = ql.TimeBasket(dates, values)
            except RuntimeError:
                out.append({"name": f"{label} / {k}", "inputs": inputs, "expected": {"construction": dict(v.THROWS)}})
                continue
            item_dates, item_values = items(basket)
            expected = {"size": len(basket), "itemDates": item_dates, "itemValues": item_values,
                        "hasDate": [d in basket for d in probes]}
            rebin_dates, rebin_values = [], []
            for b in buckets:
                try:
                    rd, rv = items(basket.rebin(b))
                except RuntimeError:
                    rd, rv = dict(v.THROWS), dict(v.THROWS)
                rebin_dates.append(rd)
                rebin_values.append(rv)
            expected["rebinDates"] = rebin_dates
            expected["rebinValues"] = rebin_values
            expected["values"] = [basket[d] for d in probes]
            out.append({"name": f"{label} / {k}", "inputs": inputs, "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/timebasket.hpp", cases)
