# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Issuer (ql/experimental/credit/issuer.hpp, .cpp), which the wheel doesn't expose, each output
computed by the C++ harness (tools/cxx/src/experimental/credit/issuer.cpp) on
tools/vectors/gen/cxx/experimental/credit/defaultevent.py's specs.

Three issuers under 31 August 2006: one of two keys on two flat curves with past and programmed events (a failure to
pay, a bankruptcy, a restructuring, each on its own date: QuantLib's event set keeps one event per date, which nanoql
doesn't, the S-xcr plan's XR8 (iv), held in Divergences/IssuerEventsOnOneDateTests.cs), one of no event, one of no
curve. For each: the default probability on each key's curve at a sample of dates, and on a key it doesn't hold,
refused; the events matching each key between pairs of dates, the first (defaultedBetween) and all (defaultsBetween),
with and without the reference date's events. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/IssuerCxxVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


pf = _load("cxx_defaultevent", "cxx/experimental/credit/defaultevent.py")

TODAY = pf.TODAY
K1 = pf.TEST_KEY
K2 = pf.na_key("USD", pf.SNR_FOR, (30, pf.DAYS), 1e6)
K3 = pf.key([pf.atomic(pf.BANKRUPTCY)], "EUR", pf.SEC_DOM)
EVENTS = [
    pf.event("FailureToPayEvent", TODAY - 200, "EUR", pf.SEC_DOM, TODAY - 150, recovery=0.4, amount=2e6),
    pf.event("BankruptcyEvent", TODAY - 100, "USD", pf.SNR_FOR, TODAY - 50,
             recoveries=[(s, 0.35) for s in pf.SENIORITIES]),
    pf.event("DefaultEvent", TODAY, "EUR", pf.SEC_DOM, type_=pf.atomic(pf.RESTRUCTURING, pf.CR)),
    pf.event("BankruptcyEvent", TODAY + 300, "EUR", pf.SEC_DOM),
]
ISSUERS = [
    ("two keys with events", pf.issuer([(K1, pf.flat(0.01)), (K2, pf.flat(0.035, TODAY - 10))], EVENTS)),
    ("one key, no event", pf.issuer([(K3, pf.flat(0.002))])),
    ("no curve, events", pf.issuer([], EVENTS[:2])),
]
KEYS = [K1, K2, K3, pf.na_key("EUR", pf.NO_SENIORITY, (0, pf.DAYS), 1.0)]
DATES = [TODAY, TODAY + 1, TODAY + 365, TODAY + 3653]
WINDOWS = [(TODAY - 365, TODAY), (TODAY - 200, TODAY - 100), (TODAY - 201, TODAY - 99), (TODAY - 100, TODAY + 400),
           (TODAY, TODAY + 300), (TODAY - 1000, TODAY + 1000)]


def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        for name, issuer in ISSUERS:
            with H.case_settings(TODAY.serialNumber()) as st:
                dates = [d.serialNumber() for d in DATES]
                expected = {
                    "defaultProbability": [call("Issuer::defaultProbability", issuer, k, dates) for k in KEYS],
                    "defaultedBetween": [call("Issuer::defaultedBetween", issuer, s.serialNumber(), e.serialNumber(),
                                              k, inc) for s, e in WINDOWS for k in KEYS for inc in (False, True)],
                    "defaultsBetween": [call("Issuer::defaultsBetween", issuer, s.serialNumber(), e.serialNumber(),
                                             k, inc) for s, e in WINDOWS for k in KEYS for inc in (False, True)]}
                out.append({"name": name,
                            "inputs": {"settings": st, "issuer": issuer, "keys": KEYS, "dates": dates,
                                       "windows": [[s.serialNumber(), e.serialNumber()] for s, e in WINDOWS]},
                            "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/credit/issuer.hpp", cases)
