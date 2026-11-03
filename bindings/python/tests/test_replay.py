# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The Python replayer's own rules (docs/bindings.md, "The vector suite through each binding" and "Checking the
bindings"): a new object's class is compared without a file-local class's compiler prefix, whose hash is of its source
file's path; the pool the sweep calls with is the same whatever the recording's order of facts; and the sweep over the
test exports' functions, through their members, after the replay and its report, each of its reds made once: code 99,
code 9, a Python-layer error, a record pair's C function disagreeing with ==, an exclusion naming no target, a call past
the limit, and a standing run with no surface, its folder kept after a red and removed after a clean run. Each sweep
runs in a child process, so its calls leave this one's state alone."""

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
CLEAN = {THROW: "a defect's kind by design"}

# A sweep of the test exports in a child process, an empty trace's replay, after the setup's lines: its reds' count
# printed.
CHILD = """
import sys
import replay
excluded, planted = {excluded!r}, {planted!r}
{setup}
red = replay.sweep(replay.Replay(), set(), sys.argv[1], "nqlt", limit={limit!r}, excluded=excluded, planted=planted)
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


class Sweep(unittest.TestCase):
    def setUp(self):
        self.work = Path(tempfile.mkdtemp(prefix="nanoql-sweep-test-"))
        self.outcomes = self.work / "outcomes.tsv"
        self.trace = self.work / "empty.jsonl"
        self.trace.write_text("", encoding="utf-8")

    def tearDown(self):
        shutil.rmtree(self.work, ignore_errors=True)

    def env(self):
        # The child's temporary folders, the sandbox among them, under this test's.
        return {**os.environ, "PYTHONPATH": os.pathsep.join([str(HERE), os.environ.get("PYTHONPATH", "")]),
                "TMP": str(self.work), "TEMP": str(self.work), "TMPDIR": str(self.work)}

    def run_child(self, excluded, planted=None, limit=30.0, setup=""):
        code = CHILD.format(excluded=excluded, planted=planted or {}, limit=limit, setup=setup)
        return subprocess.run([sys.executable, "-c", code, str(self.outcomes)], capture_output=True, text=True,
                              cwd=self.work, env=self.env(), timeout=600)

    def child(self, excluded, planted=None, setup=""):
        p = self.run_child(excluded, planted, setup=setup)
        self.assertEqual(p.returncode, 0, p.stderr[-2000:])
        return p.stdout, self.rows()

    def rows(self):
        lines = [line.split("\t") for line in self.outcomes.read_text(encoding="utf-8").splitlines()]
        return {line[0]: line for line in lines}

    def sandboxes(self):
        return [d for d in self.work.iterdir() if d.name.startswith("nanoql-sweep-") and d.is_dir()]

    def test_a_clean_run_writes_each_targets_outcome_and_removes_its_folder(self):
        out, rows = self.child(CLEAN)
        self.assertIn("reds 0", out)
        self.assertIn("1 excluded", out)
        self.assertEqual(rows[THROW][1], "excluded")
        self.assertTrue(all(r[1] in ("returned", "refused", "unreached", "excluded") for r in rows.values()))
        # The prefix's experimental module is imported, its functions swept with the stable ones; the objects the
        # sweep makes through the members join the pool, so an echo's own members and its record pair are reached.
        self.assertIn("nqlt_Walk_new", rows)
        self.assertEqual(rows["nqlt_Sleeper_Sleep"][1], "returned")
        self.assertEqual(rows["nqlt_EchoObject_get_Value"][1], "returned")
        self.assertEqual(rows["nqlt_EchoObject_op_Equality"][1:4], ["returned", "returned", "bool"])
        self.assertGreater(sum(r[1] == "returned" for r in rows.values()), 100)
        self.assertEqual(self.sandboxes(), [])

    def test_code_99_reds_in_any_attempt_and_keeps_the_folder(self):
        out, rows = self.child({})
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

    def test_an_exclusion_naming_no_target_reds(self):
        out, _ = self.child({**CLEAN, "nqlt_NoSuchFunction": "a planted entry"})
        self.assertIn("reds 1", out)
        self.assertIn("RED nqlt_NoSuchFunction: stale exclusion", out)

    def test_a_replay_and_its_report_come_before_the_sweep(self):
        # One fact calling one of the test exports' functions: replayed and reported, then swept, the function the trace
        # calls no target.
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
        self.assertNotIn("nqlt_Echo_Double", rows)
        self.assertIn("nqlt_Echo_Int", rows)
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


if __name__ == "__main__":
    unittest.main()
