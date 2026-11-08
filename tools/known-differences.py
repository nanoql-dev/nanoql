# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""A test run on a system beside Windows x64, the reference, read against that system's known differences
(docs/porting-playbook.md §7.3): the tests that fail there from the platform's floating point, a result's last bits,
from the C runtime's maths functions or the processor, which a calculation can magnify past its bound, or on Arm64 a
NaN's sign. The run passes when it finished, its exit code is the test runner's for its outcome (0 with no failure, 2
with some), its test projects' closing lines agree with its summary, and every failing test is on the list. A listed
test that passed is reported, for the list to drop, and doesn't fail it. The tests' output is read as a release's notes
read it (tools/release/notes.py), each failing test named whole, a theory's row with its arguments.

Usage: python -I tools/known-differences.py <tests output> <list> --exit-code <code>
       python -I tools/known-differences.py <tests output> <list> --write
       python -I tools/known-differences.py --self-test

A list holds one test per line, as the test runner names it, after its "#" comment lines. --write replaces its tests
with the output's failing ones and keeps its comment lines. --self-test checks the rules on sample outputs.
"""

import importlib.util
import pathlib
import re
import sys
import tempfile

REPO = pathlib.Path(__file__).resolve().parents[1]
_spec = importlib.util.spec_from_file_location("notes", REPO / "tools" / "release" / "notes.py")
notes = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(notes)

# The test runner's exit codes (https://aka.ms/testingplatform/exitcodes): 0 when every test passed, 2 when one failed.
PASSED, FAILED = 0, 2
# A test project's closing line: "<path>.dll (<framework>|<architecture>) passed (<duration>)", or "failed with <n>
# error(s) (<duration>)"; any other word there, a crash's or an abort's, fails the run.
PROJECT = re.compile(r"^\S.*\.dll \([^)]*\) (passed|failed with (\d+) error\(s\)|.+?) \([0-9hms ]+\)\s*$", re.M)
ESCAPES = re.compile(r"\x1b\[[0-?]*[ -/]*[@-~]")


def listed(path: pathlib.Path) -> tuple[list[str], list[str]]:
    """A list's comment lines and its tests."""
    lines = path.read_text(encoding="utf-8").splitlines()
    head = [line for line in lines if line.startswith("#")]
    tests = [line for line in lines if line.strip() and not line.startswith("#")]
    return head, tests


def check(output: pathlib.Path, tests: list[str], exit_code: str) -> tuple[list[str], list[str], str]:
    """Why the tests' output doesn't pass, the listed tests that passed, and its counts."""
    problems = [f"the list names {t} twice" for t in sorted({t for t in tests if tests.count(t) > 1})]
    read = notes.summary(output)
    if isinstance(read, str):
        return problems + [f"the tests' output: {read}"], [], ""
    total, failed, names = read
    text = ESCAPES.sub("", output.read_text(encoding="utf-8", errors="replace"))
    projects = PROJECT.findall(text)
    if not projects:
        problems.append("the tests' output has no test project's closing line")
    problems += [f"a test project {status}" for status, _ in projects
                 if status != "passed" and not status.startswith("failed with")]
    errors = sum(int(n) for _, n in projects if n)
    if projects and errors != failed:
        problems.append(f"the test projects' errors are {errors}, the summary's failures {failed}")
    expected = FAILED if failed else PASSED
    if exit_code != str(expected):
        problems.append(f"the test runner exited {exit_code}, not {expected} for {failed} failing")
    problems += [f"not a known difference: {n}" for n in names if n not in tests]
    return problems, sorted(set(tests) - set(names)), f"{total:,} tests, {failed} failing"


def annotate(kind: str, message: str) -> str:
    """A GitHub Actions annotation, which a local run prints as text."""
    return f"::{kind}::" + message.replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")


def main(argv: list[str]) -> int:
    if argv == ["--self-test"]:
        return self_test()
    if len(argv) == 3 and argv[2] == "--write":
        output, path = pathlib.Path(argv[0]), pathlib.Path(argv[1])
        read = notes.summary(output)
        if isinstance(read, str):
            raise SystemExit(f"{output}: {read}")
        head, _ = listed(path)
        path.write_text("\n".join(head + read[2]) + "\n", encoding="utf-8", newline="\n")
        print(f"{path}: {len(read[2])} tests written")
        return 0
    if len(argv) != 4 or argv[2] != "--exit-code":
        raise SystemExit(__doc__.split("\n\n")[1])
    output, path = pathlib.Path(argv[0]), pathlib.Path(argv[1])
    _, tests = listed(path)
    problems, passed, reading = check(output, tests, argv[3])
    for t in passed:
        print(annotate("warning", f"a known difference passed, for {path.name} to drop: {t}"))
    for p in problems:
        print(annotate("error", p))
    if problems:
        print(f"{path.name}: {reading or 'unread'}; {len(problems)} problems")
        return 1
    print(f"{path.name}: {reading}, each a known difference ({len(tests)} listed, {len(passed)} of them passed)")
    return 0


SAMPLE = """Running tests from /r/NanoQL.Tests.dll (net10.0|x64)
failed A.B.C(x: 1, y: "two") (12ms)
  Assert.Equal() Failure
  from /r/NanoQL.Tests.dll (net10.0|x64)
failed A.B.D (3ms)
/r/NanoQL.Tests.dll (net10.0|x64) failed with 2 error(s) (1m 2s 3ms)
Test run summary: Failed!
  total: 10
  failed: 2
  succeeded: 8
  skipped: 0
  duration: 1m 2s 3ms
"""
GREEN = """Running tests from /r/NanoQL.Tests.dll (net10.0|x64)
/r/NanoQL.Tests.dll (net10.0|x64) passed (1m 2s 3ms)
Test run summary: Passed!
  total: 10
  failed: 0
  succeeded: 10
  skipped: 0
  duration: 1m 2s 3ms
"""


def self_test() -> int:
    both = ['A.B.C(x: 1, y: "two")', "A.B.D"]
    crash = SAMPLE.replace("failed with 2 error(s) (1m 2s 3ms)", "crashed (1m 2s 3ms)")
    cases = [
        ("the failures listed", SAMPLE, both, "2", 0, 0),
        ("an ANSI-coloured output", "\x1b[31m" + SAMPLE.replace("\n", "\x1b[0m\n"), both, "2", 0, 0),
        ("a failure not listed", SAMPLE, both[:1], "2", 1, 0),
        ("a listed test passing", SAMPLE, both + ["A.B.E"], "2", 0, 1),
        ("a green run, its listed tests passing", GREEN, both, "0", 0, 2),
        ("the test runner's crash code", SAMPLE, both, "7", 1, 0),
        ("no exit code", SAMPLE, both, "none", 1, 0),
        ("a passing exit code with failures", SAMPLE, both, "0", 1, 0),
        ("a crashed test project", crash, both, "2", 2, 0),
        ("no summary", "Running tests from /r/NanoQL.Tests.dll (net10.0|x64)\n", both, "2", 1, 0),
        ("a test listed twice", SAMPLE, both + both[1:], "2", 1, 0),
    ]
    bad = 0
    with tempfile.TemporaryDirectory() as d:
        for name, text, tests, code, n_problems, n_passed in cases:
            out = pathlib.Path(d) / "tests.txt"
            out.write_text(text, encoding="utf-8")
            problems, passed, _ = check(out, tests, code)
            ok = len(problems) == n_problems and len(passed) == n_passed
            bad += not ok
            print(f"{'ok ' if ok else 'BAD'} {name}: {len(problems)} problems, {len(passed)} listed passing")
    print(f"self-test: {len(cases) - bad} of {len(cases)} as expected")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
