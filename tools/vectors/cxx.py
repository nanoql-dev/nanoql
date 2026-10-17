# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The C++ harness's client (docs/porting-playbook.md §7.2): harness generators under tools/vectors/gen/cxx/ call
QuantLib built from its pinned sources through it.

    import cxx
    with cxx.connect() as H:
        H.call("Date::isLeap", 2024)

connect() starts the executable NANOQL_HARNESS names, which tools/cxx/build.py builds, and refuses it unless it was
built from the working tree's tools/cxx sources (their digest), against QuantLib 1.44 and Boost 1.90 with the
wheel's code options, by MSVC 19.44. A call raises RuntimeError where QuantLib throws a QuantLib::Error or any other
exception but std::out_of_range, so vectors.attempt records {"throws": true} exactly as for the wheel; IndexError
for std::out_of_range, as the wheel does; and HarnessFault, which is no RuntimeError, for a failure of the harness
itself (a malformed request, an unknown method, an argument it can't read), which stops the generator.

Usage: .venv\\Scripts\\python -I tools/vectors/cxx.py --self-test     (needs NANOQL_HARNESS)
"""
import hashlib
import json
import math
import os
import random
import struct
import subprocess
import sys

TOOLS_VECTORS = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(TOOLS_VECTORS))
CXX = os.path.join(REPO, "tools", "cxx")

BUILD = r".venv\Scripts\python -I tools\cxx\build.py <root> --quantlib <QuantLib v1.44 worktree>"

# What the harness must report (tools/cxx/build.py's toolchain, and the wheel's QuantLib build).
QUANTLIB = "1.44"
BOOST = "1_90"
OPTIONS = ["QL_FASTER_LAZY_OBJECTS"]
MSC = 1944


class HarnessFault(Exception):
    """A failure of the harness itself, or a harness that doesn't match: never read as "QuantLib throws"."""


def source_digest(root=CXX):
    """The digest tools/cxx/CMakeLists.txt compiles in: SHA-256 over "<relative path>:<file's SHA-256>\\n" for
    CMakeLists.txt and every file under src/, in sorted order."""
    rels = ["CMakeLists.txt"]
    for dp, _, fns in os.walk(os.path.join(root, "src")):
        rels += [os.path.relpath(os.path.join(dp, fn), root).replace(os.sep, "/") for fn in fns]
    lines = ""
    for rel in sorted(rels):
        with open(os.path.join(root, *rel.split("/")), "rb") as f:
            lines += f"{rel}:{hashlib.sha256(f.read()).hexdigest()}\n"
    return hashlib.sha256(lines.encode("utf-8")).hexdigest()


def check_version(info, digest):
    """Refuses a harness built from other sources, or against another QuantLib, Boost, option set or compiler."""
    if info.get("source") != digest:
        raise HarnessFault(f"the harness was built from other sources than tools/cxx ({info.get('source')}, the "
                           f"working tree gives {digest}): rebuild it with {BUILD}")
    if info.get("quantlib") != QUANTLIB:
        raise HarnessFault(f"the harness is built against QuantLib {info.get('quantlib')}, not {QUANTLIB}")
    if info.get("boost") != BOOST:
        raise HarnessFault(f"the harness is built against Boost {info.get('boost')}, not {BOOST}")
    if info.get("options") != OPTIONS:
        raise HarnessFault(f"QuantLib was built with the options {info.get('options')}, not the wheel's {OPTIONS}")
    if info.get("msc") != MSC:
        raise HarnessFault(f"the harness was compiled by _MSC_VER {info.get('msc')}, not {MSC}")


class Harness:
    def __init__(self, exe):
        self.exe = exe
        self.p = subprocess.Popen([exe], stdin=subprocess.PIPE, stdout=subprocess.PIPE)
        self.next_id = 0

    def __enter__(self):
        return self

    def __exit__(self, *exc):
        self.close()

    def exchange(self, line, rid):
        """Sends one request line and returns its response, checked for the envelope and its id."""
        self.p.stdin.write(line.encode("utf-8") + b"\n")
        self.p.stdin.flush()
        raw = self.p.stdout.readline()
        if not raw:
            raise HarnessFault(f"the harness exited on {line}")
        try:
            r = json.loads(raw.decode("utf-8"))
        except ValueError as e:
            raise HarnessFault(f"a response to {line} that isn't JSON-RPC: {raw!r}") from e
        if not isinstance(r, dict) or r.get("jsonrpc") != "2.0" or r.get("id") != rid:
            raise HarnessFault(f"a response to {line} that isn't its own: {raw.decode('utf-8').strip()}")
        return r

    def call(self, method, *params):
        """QuantLib's answer to method(*params); RuntimeError or IndexError where it throws."""
        self.next_id += 1
        line = json.dumps({"jsonrpc": "2.0", "id": self.next_id, "method": method, "params": list(params)},
                          separators=(",", ":"))
        r = self.exchange(line, self.next_id)
        if "result" in r:
            return r["result"]
        code, message = r["error"]["code"], r["error"]["message"]
        if code in (1, 2):
            raise RuntimeError(message)
        if code == 3:
            raise IndexError(message)
        raise HarnessFault(f"{code} {message} in {line}")

    def settings(self, evaluation_date, include_reference_date_events=False, include_todays_cash_flows=None,
                 enforces_todays_historic_fixings=False):
        """Sets every Settings value QuantLib reads, as vectors.settings does, and returns them as a case's
        inputs.settings. The evaluation date is a serial."""
        self.call("Settings", evaluation_date, include_reference_date_events, include_todays_cash_flows,
                  enforces_todays_historic_fixings)
        return {"evaluationDate": evaluation_date, "includeReferenceDateEvents": include_reference_date_events,
                "includeTodaysCashFlows": include_todays_cash_flows,
                "enforcesTodaysHistoricFixings": enforces_todays_historic_fixings}

    def close(self):
        if self.p.stdin and not self.p.stdin.closed:
            self.p.stdin.close()
        self.p.wait()


def connect():
    """The harness NANOQL_HARNESS names, checked against the working tree's sources and the pinned build."""
    exe = os.environ.get("NANOQL_HARNESS")
    if not exe or not os.path.isfile(exe):
        raise SystemExit(f"NANOQL_HARNESS names no harness ({exe!r}); build it with {BUILD}, then set NANOQL_HARNESS "
                         "to the path it prints")
    h = Harness(exe)
    try:
        check_version(h.call("harness.version"), source_digest())
    except BaseException:
        h.close()
        raise
    return h


def self_test():
    sys.path.insert(0, TOOLS_VECTORS)
    import vectors  # noqa: E402  (vectors.attempt, the wheel generators' own)

    results = []

    def check(name, ok):
        results.append(ok)
        print(("ok    " if ok else "FAILED ") + name)

    def raises(f, kind, text=None):
        try:
            f()
        except kind as e:
            return text is None or text in str(e)
        except Exception:  # noqa: BLE001  (any other kind is the failure being checked)
            return False
        return False

    def passes(f):
        try:
            f()
        except Exception:  # noqa: BLE001
            return False
        return True

    def bits(x):
        return struct.pack("<d", x)

    saved = os.environ.pop("NANOQL_HARNESS", None)
    check("connect() without NANOQL_HARNESS stops with the build command",
          raises(connect, SystemExit, "tools\\cxx\\build.py"))
    if saved is None:
        print("NANOQL_HARNESS is not set; build the harness with " + BUILD)
        return 1
    os.environ["NANOQL_HARNESS"] = saved

    with connect() as H:
        rng = random.Random(20261016)
        values = [struct.unpack("<d", struct.pack("<Q", rng.getrandbits(64)))[0] for _ in range(20000)]
        values = [x for x in values if math.isfinite(x)]
        values += [5e-324, -5e-324, 2.2250738585072009e-308, 2.2250738585072014e-308, 0.0, -0.0,
                   1.7976931348623157e308, -1.7976931348623157e308, 3.4028234663852886e38, 1e22, 1e23, 0.1, 1 / 3]
        back = [H.call("harness.echo", x) for x in values]
        check(f"{len(values)} doubles round-trip bit for bit through harness.echo",
              all(isinstance(b, float) and bits(b) == bits(x) for x, b in zip(values, back)))
        nonfinite = [H.call("harness.echo", x) for x in (math.nan, math.inf, -math.inf)]
        check("NaN, +inf and -inf round-trip as tokens",
              math.isnan(nonfinite[0]) and nonfinite[1] == math.inf and nonfinite[2] == -math.inf)

        thirty360 = {"type": "Thirty360", "convention": 99}
        rate = {"rate": 0.05, "dayCounter": {"type": "Actual360"}, "compounding": 1, "frequency": 1}
        recorded = {
            "a QuantLib::Error (Date(29, February, 2023))":
                lambda: H.call("Date(Day,Month,Year)", 29, 2, 2023),
            "a constructor's refusal of an argument (Date(200000) through a date-taking method)":
                lambda: H.call("InterestRate::compoundFactor(Date,Date)", rate, 46310, 200000),
            "a constructor's refusal of an argument (Thirty360 with convention 99)":
                lambda: H.call("DayCounter::name", thirty360),
            "a standard exception from QuantLib (DateParser::parseISO's std::stoi)":
                lambda: H.call("DateParser::parseISO", "2024-02-xx"),
            "a throw that is no std::exception (harness.throwNonStd)":
                lambda: H.call("harness.throwNonStd"),
        }
        for name, f in recorded.items():
            check(name + " raises RuntimeError, recorded as {\"throws\": true}",
                  raises(f, RuntimeError) and vectors.attempt(f) == {"throws": True})
        check("std::out_of_range (harness.throwOutOfRange) raises IndexError, which vectors.attempt passes on",
              raises(lambda: H.call("harness.throwOutOfRange"), IndexError)
              and raises(lambda: vectors.attempt(lambda: H.call("harness.throwOutOfRange")), IndexError))

        def call_raw(line, rid):
            r = H.exchange(line, rid)
            raise HarnessFault(f"{r['error']['code']} {r['error']['message']}" if "error" in r else "no error")

        faults = {
            "an unknown method": (lambda: H.call("Date::isLeapp", 2024), -32601),
            "a wrong argument kind": (lambda: H.call("Date::isLeap", "2024"), -32602),
            "an unknown spec type": (lambda: H.call("DayCounter::name", {"type": "Bogus"}), -32602),
            "a spec missing a key its type needs": (lambda: H.call("DayCounter::name", {"type": "Thirty360"}), -32602),
            "a key the spec's type doesn't take":
                (lambda: H.call("DayCounter::name", {"type": "Actual360", "includeLastDay": True}), -32602),
            "the harness's own exception raised inside the call":
                (lambda: H.call("harness.throwInvalidParamsInCall"), -32602),
            "a request without jsonrpc": (lambda: call_raw('{"id":0,"method":"harness.version"}', 0), -32600),
            "malformed JSON": (lambda: call_raw('{"jsonrpc":"2.0","id":', None), -32700),
        }
        for name, (f, code) in faults.items():
            check(f"{name} raises HarnessFault {code}", raises(f, HarnessFault, str(code)))
        check("a HarnessFault is no RuntimeError, so vectors.attempt never records it",
              raises(lambda: vectors.attempt(lambda: H.call("Date::isLeapp", 2024)), HarnessFault))
        check("a response with another id is refused",
              raises(lambda: H.exchange('{"jsonrpc":"2.0","id":999,"method":"harness.version"}', 1000),
                     HarnessFault, "isn't its own"))

        info = H.call("harness.version")
        digest = source_digest()
        check("the harness reports the working tree's digest, QuantLib 1.44, Boost 1_90, the wheel's options and "
              "MSVC 1944", passes(lambda: check_version(info, digest)))
        mismatches = {
            "another source digest": ("source", "0" * 64),
            "another QuantLib": ("quantlib", "1.43"),
            "another Boost": ("boost", "1_89"),
            "other options": ("options", ["QL_FASTER_LAZY_OBJECTS", "QL_HIGH_RESOLUTION_DATE"]),
            "another compiler": ("msc", 1951),
        }
        for name, (k, value) in mismatches.items():
            check(f"the client refuses {name}",
                  raises(lambda k=k, value=value: check_version({**info, k: value}, digest), HarnessFault))

    print(f"checks {sum(results)}/{len(results)}")
    return 0 if all(results) else 1


if __name__ == "__main__":
    if sys.argv[1:] != ["--self-test"]:
        raise SystemExit(__doc__)
    sys.exit(self_test())
