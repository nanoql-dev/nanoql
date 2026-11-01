# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The Java jars' check (docs/bindings.md, "Java"): the main jar holds both layers and the five notices files under
META-INF, .NET's the MIT licence, and no library; the natives jar the platform's library and the notices; the sources
and javadoc jars are there, the sources with the notices; the POM carries Central's required fields (name, description,
URL, each licence's name and URL, a developer, the SCM's URL), its licences the wheel's; and the main and natives jars
alone on a class path load the library and price a curve, with no live handle at the end; on Windows two runs load one
copy of the library, and a file of its size planted there is replaced, never loaded; elsewhere no copy is left.

Usage: python bindings/java/jar_check.py <target folder> <natives classifier>
"""

import pathlib
import re
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
import zipfile

HERE = pathlib.Path(__file__).resolve().parent
NOTICES = ["LICENSE", "NOTICE", "THIRD_PARTY_NOTICES.md", "dotnet/LICENSE.TXT", "dotnet/THIRD-PARTY-NOTICES.TXT"]
LIBRARY = {"windows": "nanoql.dll", "linux": "libnanoql.so", "macos": "libnanoql.dylib"}
POM = "{http://maven.apache.org/POM/4.0.0}"

SMOKE = """
import java.time.LocalDate;
import org.nanoql.NanoQL;
import org.nanoql.extensions.conventions.ConventionSet;
import org.nanoql.termstructures.FlatForward;
import org.nanoql.time.DayCounter;

public class Smoke {
    public static void main(String[] args) {
        try (ConventionSet qs = ConventionSet.quantLib(); DayCounter dc = qs.dayCounter("Actual365Fixed");
             FlatForward curve = new FlatForward(LocalDate.of(2023, 3, 15), 0.03, dc)) {
            double d = curve.discount(5.0);
            if (d != Math.exp(-0.15)) {
                throw new AssertionError("discount(5) = " + d);
            }
        }
        System.out.println(NanoQL.libraryPath());
        System.out.println("a curve priced from the jars, live handles " + NanoQL.liveHandles());
        System.exit(NanoQL.liveHandles() == 0 ? 0 : 1);
    }
}
"""


def names(jar: pathlib.Path) -> dict[str, int]:
    with zipfile.ZipFile(jar) as z:
        return {i.filename: i.file_size for i in z.infolist() if not i.is_dir()}


def notices(jar: pathlib.Path, found: dict[str, int]) -> None:
    missing = [n for n in NOTICES if found.get("META-INF/" + n, 0) == 0]
    assert not missing, f"{jar.name}: notices missing or empty: {missing}"
    with zipfile.ZipFile(jar) as z:
        dotnet = z.read("META-INF/dotnet/LICENSE.TXT")
    assert dotnet.lstrip().startswith(b"The MIT License") and b".NET Foundation" in dotnet, f"{jar.name}: META-INF/dotnet/LICENSE.TXT isn't .NET's MIT licence"


def pom_fields(pom: pathlib.Path) -> list[str]:
    root = ET.parse(pom).getroot()

    def text(path: str) -> str:
        e = root.find("/".join(POM + p for p in path.split("/")))
        return (e.text or "").strip() if e is not None else ""

    for field in ("groupId", "artifactId", "version", "name", "description", "url", "scm/url"):
        assert text(field), f"the POM has no {field}"
    licences = root.findall(f"{POM}licenses/{POM}license")
    assert licences and all((l.findtext(f"{POM}name") or "").strip() and (l.findtext(f"{POM}url") or "").strip() for l in licences), "a licence without its name or URL"
    developers = root.findall(f"{POM}developers/{POM}developer")
    assert developers and all((d.findtext(f"{POM}name") or "").strip() for d in developers), "no developer with a name"
    pyproject = (HERE.parent / "python" / "pyproject.toml").read_text(encoding="utf-8")
    expression = re.search(r'^license = "([^"]+)"', pyproject, re.M).group(1)  # type: ignore[union-attr]
    wheel = set(expression.split(" AND "))
    mine = {(l.findtext(f"{POM}name") or "").strip() for l in licences}
    assert mine == wheel, f"the POM's licences aren't the wheel's: missing {sorted(wheel - mine)}, unexpected {sorted(mine - wheel)}"
    return sorted(mine)


def main() -> int:
    target = pathlib.Path(sys.argv[1]).resolve()
    classifier = sys.argv[2]
    platform = classifier.removeprefix("natives-")
    version = ET.parse(HERE / "pom.xml").getroot().findtext(f"{POM}version")
    main_jar, natives_jar = target / f"nanoql-{version}.jar", target / f"nanoql-{version}-{classifier}.jar"
    sources_jar, javadoc_jar = target / f"nanoql-{version}-sources.jar", target / f"nanoql-{version}-javadoc.jar"
    for jar in (main_jar, natives_jar, sources_jar, javadoc_jar):
        assert jar.exists(), f"no {jar.name}"

    found = names(main_jar)
    notices(main_jar, found)
    for cls in ("org/nanoql/NanoObject.class", "org/nanoql/ffi/nanoql_h.class", "org/nanoql/termstructures/FlatForward.class", "org/nanoql/internal/NanoqlTypes.class"):
        assert cls in found, f"{main_jar.name}: no {cls}"
    libraries = [n for n in found if n.endswith((".dll", ".so", ".dylib"))]
    assert not libraries, f"{main_jar.name}: a library in the main jar: {libraries}"

    natives = names(natives_jar)
    notices(natives_jar, natives)
    library = f"org/nanoql/natives/{platform}/{LIBRARY[platform.split('-')[0]]}"
    assert natives.get(library, 0) > 0, f"{natives_jar.name}: no {library}"
    classes = [n for n in natives if n.endswith(".class")]
    assert not classes, f"{natives_jar.name}: classes in the natives jar: {classes[:3]}"

    sources = names(sources_jar)
    notices(sources_jar, sources)
    assert "org/nanoql/NanoObject.java" in sources and "org/nanoql/termstructures/FlatForward.java" in sources, f"{sources_jar.name}: no sources"
    javadoc = names(javadoc_jar)
    assert "index.html" in javadoc and "org/nanoql/NanoObject.html" in javadoc, f"{javadoc_jar.name}: no javadoc"
    assert not any(n.startswith("org/nanoql/ffi/") for n in javadoc), f"{javadoc_jar.name}: jextract's layer documented"

    licences = pom_fields(HERE / "pom.xml")

    with tempfile.TemporaryDirectory() as tmp:
        smoke = pathlib.Path(tmp) / "Smoke.java"
        smoke.write_text(SMOKE, encoding="utf-8")
        sep = ";" if sys.platform == "win32" else ":"
        def smoke_run() -> tuple[pathlib.Path, str]:
            run = subprocess.run(["java", "--enable-native-access=ALL-UNNAMED", "-cp", f"{main_jar}{sep}{natives_jar}", str(smoke)],
                                 capture_output=True, text=True, cwd=tmp)
            assert run.returncode == 0, f"the jars alone: exit {run.returncode}\n{run.stdout}{run.stderr}"
            *_, path, priced = run.stdout.strip().splitlines()
            return pathlib.Path(path), priced

        first, priced = smoke_run()
        second, _ = smoke_run()
        if sys.platform == "win32":
            # One copy, under its hash, reused; a file of its size and other bytes planted there is replaced, not loaded.
            assert first == second and first.parent.name.startswith("nanoql-"), f"two runs, two copies: {first}, {second}"
            assert list(first.parent.glob(first.name + "*")) == [first], f"more than the library in its folder: {list(first.parent.iterdir())}"
            with zipfile.ZipFile(natives_jar) as z:
                jar_bytes = z.read(library)
            first.write_bytes(bytes(b ^ 0x5A for b in jar_bytes))
            third, priced = smoke_run()
            assert third == first and third.read_bytes() == jar_bytes, "a planted copy was kept"
            copy = "two runs loading one copy of the library, a planted one replaced"
        else:
            # A private copy each run, deleted once loaded: nothing is left.
            assert not first.exists() and not first.parent.exists() and not second.exists(), f"a copy left: {first}, {second}"
            copy = "no copy of the library left after a run"

    print(f"the jars of nanoql {version} ({classifier}): main {len(found):,} entries, its notices, no library; natives "
          f"{library}; sources and javadoc; the POM's Central fields and its {len(licences)} licences the wheel's; {priced}; {copy}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
