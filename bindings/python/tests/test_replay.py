# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The Python replayer's own rules (docs/bindings.md, "The vector suite through each binding" and "Checking the
bindings"): a new object's class is compared without a file-local class's compiler prefix, whose hash is of its source
file's path; the pool the sweep calls with is the same whatever the recording's order of facts; and the sweep over the
test exports' functions, through their members, after the replay and its report, each of its reds made once: code 99,
code 9, a Python-layer error, a record pair's C function disagreeing with ==, an exclusion naming no function of the
layer, a call past the limit, and a standing run with no surface, its folder kept after a red and removed after a clean
run. Each kind of widened value reds the member that reads past its array or index under it alone, and that member's
checked twin refuses; the functions the trace calls are swept after the others, and an exclusion of one stands. Each
sweep runs in a child process, so its calls leave this one's state alone."""

import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

import replay
from replay import type_key

HERE = Path(__file__).resolve().parent
THROW = "nqlt_Refusals_Throw"  # code 99 on its third choice, kind 0, by design

# The test exports' members that read past an array or an index, each under one kind of widened value alone, with
# the probe its outcome line names; the plants that need a parameter's name read only when a surface names it.
PLANTS = {
    "nqlt_UncheckedReads_First": "zero:values",
    "nqlt_UncheckedReads_Second": "one:values",
    "nqlt_UncheckedReads_IntoThree": "many:values",
    "nqlt_UncheckedReads_BelowLength": "neg:index",
    "nqlt_UncheckedReads_NonNegative": "past:index",
    "nqlt_UncheckedReads_BothEmpty": "zero:*",
    "nqlt_UncheckedReads_BothOne": "one:*",
    "nqlt_UncheckedReads_BothMany": "many:*",
}
UNPLANTED = {name: "a planted unchecked read" for name in PLANTS}
CLEAN = {THROW: "a defect's kind by design", **UNPLANTED}

# A sweep of the test exports in a child process, an empty trace's replay, after the setup's lines: its reds' count
# printed.
CHILD = """
import sys
import replay
excluded, planted, called, surface = {excluded!r}, {planted!r}, {called!r}, {surface!r}
{setup}
red = replay.sweep(replay.Replay(), called, sys.argv[1], "nqlt", surface, limit={limit!r}, excluded=excluded,
                   planted=planted)
print("reds", red)
"""

# A closed object, planted for its getter.
CLOSED = """
import nqlt
o = nqlt.EchoObject(1.0, "one")
o.close()
planted["nqlt_EchoObject_get_Value"] = [o]
"""

# A record pair's C function answering the opposite of ==.
NEGATED = """
import nqlt
real = replay._F
class Negated:
    def __getitem__(self, name):
        f = real[name]
        return (lambda *a: not f(*a)) if name == "nqlt_EchoObject_op_Equality" else f
replay._F = Negated()
"""


class TypeKey(unittest.TestCase):
    def test_file_local_classes(self):
        here = "NanoQL.TermStructures.<BlackVolTermStructure>F8C1D0E7A3B5__BlackVolSmileSectionAdapter"
        elsewhere = "NanoQL.TermStructures.<BlackVolTermStructure>F04F2A9B6C7D8__BlackVolSmileSectionAdapter"
        self.assertEqual(type_key(here), type_key(elsewhere))
        self.assertEqual(type_key(here), "NanoQL.TermStructures.BlackVolSmileSectionAdapter")
        self.assertEqual(type_key("NanoQL.TermStructures.FlatForward"), "NanoQL.TermStructures.FlatForward")
        self.assertNotEqual(type_key("NanoQL.A"), type_key("NanoQL.B"))


class Pool(unittest.TestCase):
    def test_the_pool_is_the_same_whatever_the_order_of_facts(self):
        # Facts A and B both make the echo of 1.0, B the echo of 2.0 too; the recorder writes the shared echo new in the
        # fact it ran first and seen (s) in the other. Either way the pool holds the shared echo first, at its sight in
        # A, the earlier fact by name.
        replay.use_prefix("nqlt")

        def new(value, name, result):
            return (f'{{"f":"nqlt_EchoObject_new","a":["{value}","{name}"],'
                    f'"r":{{{result},"y":"NanoQL.Bind.TestTypes.EchoObject"}}}}')

        one, two = "3ff0000000000000", "4000000000000000"
        a_first = ['{"fact":"A"}', new(one, "one", '"n":1'),
                   '{"fact":"B"}', new(two, "two", '"n":2'), new(one, "one", '"s":1')]
        b_first = ['{"fact":"B"}', new(two, "two", '"n":1'), new(one, "one", '"n":2'),
                   '{"fact":"A"}', new(one, "one", '"s":2')]
        orders = []
        for lines in (a_first, b_first):
            r = replay.Replay()
            stopped = False
            for line in lines:
                if line.startswith('{"fact"'):
                    r.fact(json.loads(line)["fact"])
                    stopped = False
                elif not stopped:
                    try:
                        r.call(json.loads(line))
                    except replay.Stop:
                        stopped = True
            pool = replay.pool_of(r)
            orders.append([o.value for objs in pool.values() for o in objs])
        self.assertEqual(orders, [[1.0, 2.0], [1.0, 2.0]])


def child_env(work):
    # The child's temporary folders, the sandbox among them, under the test's.
    return {**os.environ, "PYTHONPATH": os.pathsep.join([str(HERE), os.environ.get("PYTHONPATH", "")]),
            "TMP": str(work), "TEMP": str(work), "TMPDIR": str(work)}


def run_child(work, outcomes, excluded, planted=None, limit=30.0, setup="", called=None, surface=None):
    code = CHILD.format(excluded=excluded, planted=planted or {}, called=called or set(), surface=surface, limit=limit,
                        setup=setup)
    return subprocess.run([sys.executable, "-c", code, str(outcomes)], capture_output=True, text=True, cwd=work,
                          env=child_env(work), timeout=600)


def read_rows(outcomes):
    lines = [line.split("	") for line in Path(outcomes).read_text(encoding="utf-8").splitlines()]
    return {line[0]: line for line in lines}


class Child(unittest.TestCase):
    """A child process's sweep, in a folder of its own."""

    def setUp(self):
        self.work = Path(tempfile.mkdtemp(prefix="nanoql-sweep-test-"))
        self.outcomes = self.work / "outcomes.tsv"
        self.trace = self.work / "empty.jsonl"
        self.trace.write_text("", encoding="utf-8")

    def tearDown(self):
        shutil.rmtree(self.work, ignore_errors=True)

    def env(self):
        return child_env(self.work)

    def run_child(self, excluded, planted=None, limit=30.0, setup="", called=None):
        return run_child(self.work, self.outcomes, excluded, planted, limit, setup, called)

    def child(self, excluded, planted=None, setup="", called=None):
        p = self.run_child(excluded, planted, setup=setup, called=called)
        self.assertEqual(p.returncode, 0, p.stderr[-2000:])
        return p.stdout, self.rows()

    def rows(self):
        return read_rows(self.outcomes)


class Sweep(Child):
    def sandboxes(self):
        return [d for d in self.work.iterdir() if d.name.startswith("nanoql-sweep-") and d.is_dir()]

    def test_a_clean_run_writes_each_targets_outcome_and_removes_its_folder(self):
        out, rows = self.child(CLEAN)
        self.assertIn("reds 0", out)
        self.assertIn(f"{len(CLEAN)} excluded", out)
        self.assertEqual(rows[THROW][1], "excluded")
        self.assertTrue(all(r[1] in ("returned", "refused", "unreached", "excluded") for r in rows.values()))
        # The prefix's experimental module is imported, its functions swept with the stable ones; the objects the
        # sweep makes through the members join the pool, so an echo's own members and its record pair are reached.
        self.assertIn("nqlt_Walk_new", rows)
        self.assertEqual(rows["nqlt_Sleeper_Sleep"][1], "returned")
        self.assertEqual(rows["nqlt_EchoObject_get_Value"][1], "returned")
        self.assertEqual(rows["nqlt_EchoObject_op_Equality"][1:4], ["returned", "returned", "bool"])
        self.assertGreater(sum(r[1] == "returned" for r in rows.values()), 100)
        # An array's widened attempts close its line.
        self.assertIn("many:0 returned", rows["nqlt_Echo_Doubles"][5])
        self.assertEqual(self.sandboxes(), [])

    def test_code_99_reds_in_any_attempt_and_keeps_the_folder(self):
        out, rows = self.child(UNPLANTED)
        self.assertIn("reds 1", out)
        self.assertEqual(rows[THROW][1:3], ["red", "code 99"])
        self.assertIn(f"RED {THROW}: code 99", out)
        kept = self.sandboxes()
        self.assertEqual(len(kept), 1)
        self.assertIn(f"kept for the diagnosis, {kept[0]}", out)

    def test_code_9_reds(self):
        out, rows = self.child(CLEAN, setup=CLOSED)
        self.assertIn("reds 1", out)
        self.assertEqual(rows["nqlt_EchoObject_get_Value"][1:3], ["red", "code 9"])

    def test_a_python_layer_error_reds(self):
        out, rows = self.child(CLEAN, {"nqlt_Echo_Double": ["a string, not a double"]})
        self.assertIn("reds 1", out)
        self.assertEqual(rows["nqlt_Echo_Double"][1], "red")
        self.assertTrue(rows["nqlt_Echo_Double"][2].startswith("python "), rows["nqlt_Echo_Double"])

    def test_a_record_pairs_c_function_disagreeing_with_the_languages_equality_reds(self):
        out, rows = self.child(CLEAN, setup=NEGATED)
        self.assertIn("reds 1", out)
        self.assertEqual(rows["nqlt_EchoObject_op_Equality"][1:3], ["red", "python AssertionError"])

    def test_an_exclusion_naming_no_function_of_the_layer_reds(self):
        out, _ = self.child({**CLEAN, "nqlt_NoSuchFunction": "a planted entry"})
        self.assertIn("reds 1", out)
        self.assertIn("RED nqlt_NoSuchFunction: stale exclusion", out)

    def test_the_functions_the_trace_calls_are_swept_after_the_others(self):
        out, rows = self.child(CLEAN, called={"nqlt_Echo_Doubles"})
        self.assertIn("reds 0", out)
        self.assertIn("the trace's 1 functions", out)
        doubles = rows["nqlt_Echo_Doubles"]
        self.assertEqual(doubles[1], "returned")
        self.assertEqual(doubles[4], "trace")
        self.assertIn("zero:0 returned", doubles[5])
        self.assertEqual(list(rows)[-len(CLEAN) - 1], "nqlt_Echo_Doubles")

    def test_an_exclusion_of_a_function_the_trace_calls_stands(self):
        out, rows = self.child({**CLEAN, "nqlt_Echo_Doubles": "a planted entry"}, called={"nqlt_Echo_Doubles"})
        self.assertIn("reds 0", out)
        self.assertIn("the trace's 0 functions", out)
        self.assertEqual(rows["nqlt_Echo_Doubles"][1:3], ["excluded", "a planted entry"])

    def test_a_replay_and_its_report_come_before_the_sweep(self):
        # One fact calling one of the test exports' functions: replayed and reported, then swept, the function the trace
        # calls among the trace's.
        trace = self.work / "one.jsonl"
        trace.write_text('{"fact":"Echo.Double"}\n{"f":"nqlt_Echo_Double","a":["3ff0000000000000"],"r":"3ff0000000000000"}\n',
                         encoding="utf-8")
        report = self.work / "facts.tsv"
        p = subprocess.run([sys.executable, str(HERE / "replay.py"), str(trace), str(report), "--sweep",
                            "outcomes.tsv", "--prefix", "nqlt"], capture_output=True, text=True, cwd=self.work,
                           env=self.env(), timeout=600)
        self.assertEqual(p.returncode, 1, p.stderr[-2000:])  # Refusals.Throw's code 99
        self.assertIn("facts replayed whole 1 of 1", p.stdout)
        self.assertEqual(report.read_text(encoding="utf-8").splitlines()[1], "Echo.Double\t1\t1\t1\t1")
        # The outcomes file given relative to the working folder lands there, not in the sandbox.
        rows = self.rows()
        self.assertEqual(rows["nqlt_Echo_Double"][4], "trace")
        self.assertEqual(rows["nqlt_Echo_Int"][4], "1")
        self.assertIn(f"RED {THROW}: code 99", p.stdout)

    def test_a_call_past_the_limit_ends_the_process_naming_it(self):
        # The sleeper planted past a limit every other call keeps well within.
        p = self.run_child(CLEAN, {"nqlt_Sleeper_Sleep": [5.0]}, limit=2.0)
        self.assertEqual(p.returncode, 1)
        self.assertNotIn("reds", p.stdout)
        before, timeout, _ = p.stderr.partition("Timeout")
        self.assertTrue(timeout, p.stderr[-2000:])
        self.assertEqual(before.strip().splitlines()[-1], "nqlt_Sleeper_Sleep")

    def test_a_standing_run_without_its_surface_says_so(self):
        moved = self.work / "a" / "b"
        moved.mkdir(parents=True)
        shutil.copy(HERE / "replay.py", moved)
        p = subprocess.run([sys.executable, str(moved / "replay.py"), str(self.trace), "--sweep", str(self.outcomes)],
                           capture_output=True, text=True, cwd=self.work, env=self.env(), timeout=600)
        self.assertEqual(p.returncode, 1)
        self.assertIn("sweep: no surface at", p.stdout)
        self.assertFalse(self.outcomes.exists())

    def test_an_option_with_no_value_and_no_trace_get_the_usage(self):
        for args in ([str(self.trace), "--sweep"], [str(self.trace), "--sweep", "--prefix", "nqlt"],
                     ["--sweep", str(self.outcomes)]):
            with self.subTest(args=args[1:]):
                p = subprocess.run([sys.executable, str(HERE / "replay.py"), *args], capture_output=True, text=True,
                                   cwd=self.work, env=self.env(), timeout=600)
                self.assertEqual(p.returncode, 2, p.stderr[-2000:])
                self.assertIn("Usage: python replay.py", p.stdout)
                self.assertNotIn("Traceback", p.stderr)
        self.assertFalse(self.outcomes.exists())


class Probes(unittest.TestCase):
    """One sweep with the plants in and a surface naming their parameters: each plant red under its probe alone, and
    each checked twin refusing it."""

    @classmethod
    def setUpClass(cls):
        work = Path(tempfile.mkdtemp(prefix="nanoql-sweep-test-"))
        try:
            def params(probe):
                return ["index"] if probe.endswith(":index") else ["a", "b"] if probe.endswith(":*") else ["values"]

            surface = work / "surface.json"
            functions = [{"name": name, "params": [{"name": x} for x in params(probe)]}
                         for plant, probe in PLANTS.items() for name in (plant, plant + "Checked")]
            surface.write_text(json.dumps({"functions": functions}), encoding="utf-8")
            p = run_child(work, work / "outcomes.tsv", {THROW: CLEAN[THROW]}, surface=str(surface))
            assert p.returncode == 0, p.stderr[-2000:]
            cls.out, cls.lines = p.stdout, read_rows(work / "outcomes.tsv")
        finally:
            shutil.rmtree(work, ignore_errors=True)

    def red_under(self, plant):
        line = self.lines[plant]
        probe = PLANTS[plant]
        self.assertEqual(line[1:3], ["red", f"code 99 ({probe})"], line)
        self.assertIn(f"RED {plant}: code 99 ({probe})", self.out)
        # Under that probe alone, and its twin refuses it.
        reds = [w for w in line[5].split("; ") if w.endswith(" code 99")]
        self.assertEqual(reds, [f"{probe} code 99"], line)
        twin = self.lines[plant + "Checked"]
        self.assertEqual(twin[1], "returned", twin)
        self.assertNotIn("code 99", twin[5])
        self.assertTrue(any(w in (f"{probe} code 2", f"{probe} code 4") for w in twin[5].split("; ")), twin)

    def test_an_empty_array(self):
        self.red_under("nqlt_UncheckedReads_First")

    def test_an_array_of_one(self):
        self.red_under("nqlt_UncheckedReads_Second")

    def test_an_array_of_many(self):
        self.red_under("nqlt_UncheckedReads_IntoThree")

    def test_a_negative_index(self):
        self.red_under("nqlt_UncheckedReads_BelowLength")

    def test_an_index_past_a_length(self):
        self.red_under("nqlt_UncheckedReads_NonNegative")

    def test_every_array_empty(self):
        self.red_under("nqlt_UncheckedReads_BothEmpty")

    def test_every_array_of_one(self):
        self.red_under("nqlt_UncheckedReads_BothOne")

    def test_every_array_of_many(self):
        self.red_under("nqlt_UncheckedReads_BothMany")

    def test_the_plants_are_the_reds(self):
        self.assertIn(f"reds {len(PLANTS)}", self.out)


if __name__ == "__main__":
    unittest.main()
