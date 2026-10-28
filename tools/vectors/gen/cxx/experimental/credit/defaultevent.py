# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's default types, default keys and default events (ql/experimental/credit/defaulttype.hpp,
.cpp, defaultprobabilitykey.hpp, .cpp, defaultevent.hpp, .cpp), which the wheel doesn't expose, each output computed
by the C++ harness (tools/cxx/src/experimental/credit/defaultevent.cpp).

- "types": every atomic type with no restructuring and the restructuring types on a restructuring, their summaries,
  their contains checks against every atomic and restructuring type, and equality on pairs; the incoherent pairs
  (a restructuring without a type, a type on another event), refused; a failure to pay's grace and amount.
- "keys": QuantLib's empty key, North American corporate keys (each restructuring type, currencies, seniorities,
  graces), keys of explicit event types, their summaries and sizes, equality on pairs (permuted types, other
  seniorities, currencies, sizes); a key with a repeated atomic type, refused.
- "events": default, failure-to-pay and bankruptcy events, unsettled, settled by a map (its seniorities read; the
  others null) or at one rate for NoSeniority (every seniority), and at one rate for a seniority, read at that
  seniority alone, since QuantLib fills the others with ISDA's conventional recoveries where nanoql settles the
  seniority named (the S-xcr plan's XR8 (iii), held in Divergences/SingleSenioritySettlementTests.cs); a bankruptcy's
  map holding every ISDA seniority, its partial maps refused by QuantLib alone and so left out; their inspectors,
  recovery reads (NoSeniority refused on a settled event), matches against types and keys at the evaluation date
  (failure to pay's grace and amount), occurrence at dates and equality; the refused constructions (a settlement
  before the event, a map without the event's seniority or with NoSeniority).

This file also holds the credit portfolio generators' shared inputs, the specs of
tools/cxx/src/experimental/credit/portfoliospec.hpp: currencies, types, keys, events, issuers, pools, baskets and loss
models, and the loss models' statistics through their basket. Read back by tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/DefaultEventCxxVectorTests.cs, whose inputs
tests/NanoQL.Tests/Vectors/PortfolioInputs.cs builds."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


dts = _load("wheel_defaulttermstructure", "termstructures/defaulttermstructure.py")

D = ql.Date
TODAY = D(31, 8, 2006)
A365 = dts.A365
DAYS, WEEKS, MONTHS, YEARS = int(ql.Days), int(ql.Weeks), int(ql.Months), int(ql.Years)

# QuantLib's enums, by value.
SEC_DOM, SNR_FOR, SUB_LT2, JR_SUB_T2, PREF_T1, NO_SENIORITY = range(6)
SENIORITIES = [SEC_DOM, SNR_FOR, SUB_LT2, JR_SUB_T2, PREF_T1]
RESTRUCTURING, BANKRUPTCY, FAILURE_TO_PAY, REPUDIATION, ACCELERATION, DEFAULT, DOWNGRADE, MERGER = range(8)
ATOMICS = list(range(8))
XR, MR, MM, CR, ANY = range(5)
RESTRUCTURINGS = list(range(5))


def atomic(a, r=XR):
    return {"atomic": a, "restructuring": r}


def failure_to_pay(grace, amount):
    return {"failureToPay": list(grace), "amount": amount}


def na_key(currency, seniority, grace=None, amount=None, restructuring=None):
    k = {"northAmericaCorp": True, "currency": currency, "seniority": seniority}
    if grace is not None:
        k["grace"] = list(grace)
    if amount is not None:
        k["amount"] = amount
    if restructuring is not None:
        k["restructuring"] = restructuring
    return k


def key(types, currency, seniority):
    return {"eventTypes": types, "currency": currency, "seniority": seniority}


def event(kind, date, currency, seniority, settlement=None, recovery=None, recoveries=None, type_=None, amount=None):
    e = {"kind": kind, "date": date.serialNumber(), "currency": currency, "seniority": seniority,
         "settlementDate": None if settlement is None else settlement.serialNumber()}
    if recovery is not None:
        e["recovery"] = recovery
    if recoveries is not None:
        e["recoveries"] = [list(p) for p in recoveries]
    if type_ is not None:
        e["type"] = type_
    if amount is not None:
        e["amount"] = amount
    return e


def flat(h, reference=TODAY, dc=A365):
    return dts.flat_spec(reference, h, dc)


# The tests' key: a North American corporate key in EUR, senior secured, no grace, an amount of 1.
TEST_KEY = na_key("EUR", SEC_DOM, (0, DAYS), 1.0)


def issuer(curves, events=()):
    return {"curves": [{"key": k, "curve": c} for k, c in curves], "events": list(events)}


def pool(names):
    """names: [(name, issuer, key or None)]."""
    return [{"name": n, "issuer": i, "key": k} for n, i, k in names]


def basket(ref, notionals, pool_spec, attachment=0.0, detachment=1.0):
    return {"refDate": ref.serialNumber(), "notionals": list(notionals), "pool": pool_spec,
            "attachment": attachment, "detachment": detachment}


def flat_pool(hazards, key_spec=TEST_KEY, reference=TODAY, events=None):
    """One name per hazard rate, Name0, Name1, ..., in name order, each on its flat curve under the key."""
    events = events or {}
    return pool([(f"Name{i}", issuer([(key_spec, flat(h, reference))], events.get(i, ())), key_spec)
                 for i, h in enumerate(hazards)])


def constant_loss(copula, recoveries, correlation=None, factor_weights=None, t_orders=None, integration=0):
    m = {"kind": "ConstantLossModel", "copula": copula, "integration": integration, "recoveries": list(recoveries)}
    if correlation is not None:
        m["correlation"] = correlation
        m["nVariables"] = len(recoveries)
    else:
        m["factorWeights"] = factor_weights
    if t_orders is not None:
        m["tOrders"] = list(t_orders)
    return m


def model_statistics(call, b, m, dates, levels=(), shortfalls=(), fractions=(), events=False, nth=False, splits=(),
                     distribution=False, recoveries=False, refused=()):
    """A loss model's statistics through the basket it is set on (tools/cxx/src/experimental/credit/basket.cpp): the
    expected tranche loss at each date; the percentile at each date and level, the shortfall at each date and level,
    the probability over each loss fraction at each date; the event counts (at least n events for n from 0 to the size
    plus one) and the n-th event's probabilities for n from 1 to the size; the split of each loss level; the loss
    distribution at each date; each name's recovery at the first date; and the statistics refused, each at the last
    date. The inputs are tests/NanoQL.Tests/Vectors/LossModelStatistics.cs's."""
    n = len(b["notionals"])
    ds = [d.serialNumber() for d in dates]
    inputs = {"basket": b, "model": m, "dates": ds, "levels": list(levels), "shortfalls": list(shortfalls),
              "fractions": list(fractions), "splits": list(splits), "refused": list(refused)}
    expected = {"expectedTrancheLoss": [call("Basket::expectedTrancheLoss", b, m, d) for d in ds]}
    if levels:
        expected["percentile"] = [call("Basket::percentile", b, m, d, p) for d in ds for p in levels]
    if shortfalls:
        expected["expectedShortfall"] = [call("Basket::expectedShortfall", b, m, d, p) for d in ds for p in shortfalls]
    if fractions:
        expected["probOverLoss"] = [call("Basket::probOverLoss", b, m, d, f) for d in ds for f in fractions]
    if events:
        expected["probAtLeastNEvents"] = [call("Basket::probAtLeastNEvents", b, m, k, d)
                                          for d in ds for k in range(n + 2)]
    if nth:
        expected["probsBeingNthEvent"] = [call("Basket::probsBeingNthEvent", b, m, k, d)
                                          for d in ds for k in range(1, n + 1)]
    if splits:
        expected["splitVaRLevel"] = [call("Basket::splitVaRLevel", b, m, d, x) for d in ds for x in splits]
    if distribution:
        expected["lossDistribution"] = [call("Basket::lossDistribution", b, m, d) for d in ds]
    if recoveries:
        expected["recoveryRate"] = [call("Basket::recoveryRate", b, m, ds[0], i) for i in range(n)]
    args = {"probsBeingNthEvent": (1,), "expectedTrancheLoss": (), "probOverLoss": (0.5,), "percentile": (0.9,),
            "expectedShortfall": (0.9,), "splitVaRLevel": (1.0,), "lossDistribution": (), "recoveryRate": (0,),
            "probAtLeastNEvents": (1,), "defaultCorrelation": (0, 1)}
    expected["refused"] = []
    for r in refused:
        a = args[r]
        if r in ("probsBeingNthEvent", "probAtLeastNEvents"):
            expected["refused"].append(call("Basket::" + r, b, m, a[0], ds[-1]))
        else:
            expected["refused"].append(call("Basket::" + r, b, m, ds[-1], *a))
    return inputs, expected


TYPES = ([atomic(a) for a in ATOMICS if a != RESTRUCTURING]
         + [atomic(RESTRUCTURING, r) for r in RESTRUCTURINGS if r != XR])
INCOHERENT = [atomic(RESTRUCTURING, XR), atomic(BANKRUPTCY, MR), atomic(FAILURE_TO_PAY, ANY)]
FAILURES = [failure_to_pay((0, DAYS), 1.0), failure_to_pay((30, DAYS), 1e6), failure_to_pay((2, MONTHS), 5e5)]

KEYS = [
    {},
    TEST_KEY,
    na_key("EUR", SEC_DOM),
    na_key("USD", SNR_FOR, (30, DAYS), 1e6, XR),
    na_key("USD", SNR_FOR, (30, DAYS), 1e6, MM),
    na_key(None, SEC_DOM, (0, DAYS), 1.0),
    na_key("GBP", NO_SENIORITY, (1, WEEKS), 2e6, MR),
    key([atomic(BANKRUPTCY), atomic(FAILURE_TO_PAY)], "EUR", SEC_DOM),
    key([atomic(FAILURE_TO_PAY), atomic(BANKRUPTCY)], "EUR", SEC_DOM),
    key([atomic(BANKRUPTCY)], "EUR", SEC_DOM),
    key([atomic(BANKRUPTCY)], "EUR", SNR_FOR),
    key([atomic(BANKRUPTCY)], "USD", SEC_DOM),
    key([atomic(RESTRUCTURING, MM), failure_to_pay((0, DAYS), 1.0)], None, NO_SENIORITY),
    key([], "EUR", SUB_LT2),
]
REFUSED_KEYS = [key([atomic(BANKRUPTCY), atomic(BANKRUPTCY)], "EUR", SEC_DOM),
                key([failure_to_pay((0, DAYS), 1.0), atomic(FAILURE_TO_PAY)], "EUR", SEC_DOM)]

E1 = TODAY - 400
EVENTS = [
    # name, event, the seniorities its recovery is read at (None: all five and NoSeniority)
    ("unsettled default", event("DefaultEvent", E1, "EUR", SEC_DOM, type_=atomic(DEFAULT)), None),
    ("unsettled restructuring", event("DefaultEvent", E1, "EUR", SNR_FOR, type_=atomic(RESTRUCTURING, MM)), None),
    ("default settled by a map", event("DefaultEvent", E1, "EUR", SEC_DOM, E1 + 30,
                                       recoveries=[(SEC_DOM, 0.45), (SUB_LT2, 0.1)], type_=atomic(DEFAULT)), None),
    ("default settled for NoSeniority", event("DefaultEvent", E1, "USD", NO_SENIORITY, E1 + 10, recovery=0.37,
                                              type_=atomic(ACCELERATION)), None),
    ("default settled at one rate", event("DefaultEvent", E1, "EUR", SNR_FOR, E1 + 10, recovery=0.3,
                                          type_=atomic(REPUDIATION)), [SNR_FOR]),
    ("failure to pay settled by a map", event("FailureToPayEvent", TODAY - 45, "EUR", SEC_DOM, TODAY - 5,
                                              recoveries=[(SEC_DOM, 0.6)], amount=2e6), None),
    ("failure to pay at one rate", event("FailureToPayEvent", TODAY - 20, "EUR", SUB_LT2, TODAY - 1, recovery=0.3,
                                         amount=5e5), [SUB_LT2]),
    ("unsettled failure to pay", event("FailureToPayEvent", TODAY - 31, "USD", SEC_DOM, amount=1e6), None),
    ("failure to pay on the evaluation date", event("FailureToPayEvent", TODAY, "EUR", SEC_DOM, amount=1e7), None),
    ("bankruptcy settled for every seniority", event("BankruptcyEvent", E1, "EUR", SEC_DOM, E1 + 60,
                                                     recoveries=[(s, 0.1 * (s + 1)) for s in SENIORITIES]), None),
    ("bankruptcy settled for NoSeniority", event("BankruptcyEvent", E1, None, NO_SENIORITY, E1 + 60,
                                                 recovery=0.25), None),
    ("bankruptcy at one rate", event("BankruptcyEvent", E1, "EUR", PREF_T1, E1 + 60, recovery=0.05), [PREF_T1]),
    ("unsettled bankruptcy", event("BankruptcyEvent", TODAY + 10, "GBP", SEC_DOM), None),
]
REFUSED_EVENTS = [
    event("DefaultEvent", E1, "EUR", SEC_DOM, E1 - 1, recovery=0.4, type_=atomic(DEFAULT)),
    event("DefaultEvent", E1, "EUR", SEC_DOM, E1 - 1, recoveries=[(SEC_DOM, 0.4)], type_=atomic(DEFAULT)),
    event("DefaultEvent", E1, "EUR", SEC_DOM, E1 + 1, recoveries=[(SNR_FOR, 0.4)], type_=atomic(DEFAULT)),
    event("DefaultEvent", E1, "EUR", SEC_DOM, E1 + 1, recoveries=[(SEC_DOM, 0.4), (NO_SENIORITY, 0.3)],
          type_=atomic(DEFAULT)),
    event("DefaultEvent", E1, "EUR", SEC_DOM, recoveries=[(NO_SENIORITY, 0.3)], type_=atomic(DEFAULT)),
    event("FailureToPayEvent", E1, "EUR", SEC_DOM, E1 - 5, recovery=0.4, amount=1e6),
    event("BankruptcyEvent", E1, "EUR", SEC_DOM, E1 - 5, recovery=0.4),
]
MATCH_TYPES = TYPES + FAILURES + [failure_to_pay((1, YEARS), 1.0), failure_to_pay((0, DAYS), 1e9)]
MATCH_KEYS = KEYS[1:] + [na_key("EUR", SNR_FOR, (0, DAYS), 1.0), na_key("EUR", NO_SENIORITY, (0, DAYS), 1.0),
                         na_key("EUR", SUB_LT2, (30, DAYS), 1e6), na_key("USD", SEC_DOM, (30, DAYS), 1e6)]
OCCURRENCE = [E1 - 1, E1, E1 + 1, TODAY - 45, TODAY - 20, TODAY, TODAY + 10, TODAY + 11]


def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        with H.case_settings(TODAY.serialNumber()) as st:
            out.append({"name": "types",
                        "inputs": {"settings": st, "types": TYPES, "incoherent": INCOHERENT, "failures": FAILURES,
                                   "atomics": ATOMICS, "restructurings": RESTRUCTURINGS},
                        "expected": {
                            "inspect": [call("DefaultType::inspect", t) for t in TYPES + INCOHERENT + FAILURES],
                            "containsDefaultType": [call("DefaultType::containsDefaultType", t, a)
                                                    for t in TYPES + FAILURES for a in ATOMICS],
                            "containsRestructuringType": [call("DefaultType::containsRestructuringType", t, r)
                                                          for t in TYPES + FAILURES for r in RESTRUCTURINGS],
                            "equals": [call("DefaultType::equals", t1, t2) for t1 in TYPES + FAILURES
                                       for t2 in TYPES + FAILURES],
                            "failureToPay": [call("FailureToPay::inspect", t) for t in FAILURES]}})
        with H.case_settings(TODAY.serialNumber()) as st:
            all_keys = KEYS + REFUSED_KEYS
            out.append({"name": "keys",
                        "inputs": {"settings": st, "keys": KEYS, "refused": REFUSED_KEYS},
                        "expected": {
                            "inspect": [call("DefaultProbKey::inspect", k) for k in all_keys],
                            "equals": [call("DefaultProbKey::equals", k1, k2) for k1 in KEYS for k2 in KEYS]}})
        for name, e, read in EVENTS:
            with H.case_settings(TODAY.serialNumber()) as st:
                seniorities = read if read is not None else SENIORITIES + [NO_SENIORITY]
                settled = e["settlementDate"] is not None
                expected = {
                    "inspect": call("DefaultEvent::inspect", e),
                    "recoveryRate": [call("DefaultEvent::recoveryRate", e, s) for s in seniorities],
                    "matchesEventType": [call("DefaultEvent::matchesEventType", e, t) for t in MATCH_TYPES],
                    "matchesDefaultKey": [call("DefaultEvent::matchesDefaultKey", e, k) for k in MATCH_KEYS],
                    "hasOccurred": [call("DefaultEvent::hasOccurred", e, d.serialNumber(), inc)
                                    for d in OCCURRENCE for inc in (False, True)],
                    "equals": [call("DefaultEvent::equals", e, other) for _, other, _ in EVENTS]}
                if settled:
                    expected["settlementRecoveryRate"] = [call("DefaultSettlement::recoveryRate", e, s)
                                                          for s in seniorities]
                if e["kind"] == "FailureToPayEvent":
                    expected["amountDefaulted"] = call("FailureToPayEvent::amountDefaulted", e)
                out.append({"name": name,
                            "inputs": {"settings": st, "event": e, "seniorities": seniorities,
                                       "matchTypes": MATCH_TYPES, "matchKeys": MATCH_KEYS,
                                       "occurrence": [d.serialNumber() for d in OCCURRENCE],
                                       "others": [x for _, x, _ in EVENTS]},
                            "expected": expected})
        with H.case_settings(TODAY.serialNumber()) as st:
            out.append({"name": "refused events",
                        "inputs": {"settings": st, "events": REFUSED_EVENTS},
                        "expected": {"inspect": [call("DefaultEvent::inspect", e) for e in REFUSED_EVENTS]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/credit/defaultevent.hpp", cases)
