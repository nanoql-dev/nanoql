# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's RecoveryRateQuote and ConstantRecoveryModel (ql/experimental/credit/recoveryratequote.hpp,
.cpp, recoveryratemodel.hpp, .cpp), which the wheel doesn't expose, each output computed by the C++ harness
(tools/cxx/src/experimental/credit/recoveryratequote.cpp) on tools/vectors/gen/cxx/experimental/credit/defaultevent.py's
specs: ISDA's conventional recovery of each of the five ISDA seniorities (NoSeniority reads past QuantLib's table and is
left out, held in Divergences/ConventionalRecoveryNoSeniorityTests.cs) and its map; quotes of values in [0, 1] and of
each seniority, and values outside it, refused; and the constant model's recovery at dates and keys, and whether it
applies to each seniority. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/RecoveryRateQuoteCxxVectorTests.cs."""
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
ALL = pf.SENIORITIES + [pf.NO_SENIORITY]
QUOTES = [(0.0, pf.SEC_DOM), (0.4, pf.SNR_FOR), (1.0, pf.NO_SENIORITY), (0.123456789, pf.PREF_T1), (-1e-12, pf.SEC_DOM),
          (1.0000001, pf.SEC_DOM)]
MODELS = [(0.4, pf.NO_SENIORITY), (0.25, pf.SUB_LT2)]
KEYS = [{}, pf.TEST_KEY]
DATES = [TODAY, TODAY + 3650]


def cases():
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        with H.case_settings(TODAY.serialNumber()) as st:
            return [{"name": "quotes and models",
                     "inputs": {"settings": st, "seniorities": pf.SENIORITIES, "quotes": [list(q) for q in QUOTES],
                                "models": [list(m) for m in MODELS], "keys": KEYS,
                                "dates": [d.serialNumber() for d in DATES], "asked": ALL},
                     "expected": {
                         "conventionalRecovery": [call("RecoveryRateQuote::conventionalRecovery", s)
                                                  for s in pf.SENIORITIES],
                         "isdaConvMap": call("RecoveryRateQuote::makeIsdaConvMap"),
                         "quote": [call("RecoveryRateQuote::inspect", q, s) for q, s in QUOTES],
                         "recoveryValue": [call("ConstantRecoveryModel::recoveryValue", r, s, d.serialNumber(), k)
                                           for r, s in MODELS for d in DATES for k in KEYS],
                         "appliesToSeniority": [call("ConstantRecoveryModel::appliesToSeniority", r, s, a)
                                                for r, s in MODELS for a in ALL]}}]


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/credit/recoveryratequote.hpp", cases)
