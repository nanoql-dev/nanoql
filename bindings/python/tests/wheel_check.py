# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The installed wheel's check (docs/bindings.md, "Python"): it imports, prices a swap through a bound engine, ends with
no live handle once Python collects its objects, its metadata names every licence and notices file the wheel carries
(pyproject.toml's license and license-files), and its description is the package's README, as Markdown.

Usage: python wheel_check.py <pyproject.toml>   (in a fresh environment the wheel was installed into)
"""

import datetime
import gc
import importlib.metadata
import pathlib
import sys
import tomllib


def main() -> int:
    import nanoql
    from nanoql import ConventionSet, DiscountingSwapEngine, EvaluationContext, FlatForward, MakeVanillaSwap, Period, TimeUnit

    assert "site-packages" in nanoql.__file__ or "dist-packages" in nanoql.__file__, f"not the installed package: {nanoql.__file__}"
    qs = ConventionSet.quant_lib()
    today = datetime.date(2026, 10, 30)
    curve = FlatForward(today, 0.03, qs.day_counter("Actual365Fixed"))
    swap = MakeVanillaSwap(Period(5, TimeUnit.Years), qs.ibor_index("Euribor6M", curve)).with_pricing_engine(
        DiscountingSwapEngine.bind(curve)).to_vanilla_swap(EvaluationContext(today))
    rate = swap.fixed_rate
    assert 0.029 < rate < 0.032, rate
    del swap, curve, qs
    gc.collect()
    live = nanoql.live_handles()
    assert live == 0, f"{live} live handles after collection"

    project = tomllib.loads(open(sys.argv[1], encoding="utf-8").read())["project"]
    meta = importlib.metadata.distribution("nanoql")
    assert meta.metadata["License-Expression"] == project["license"], meta.metadata["License-Expression"]
    assert meta.metadata.get_all("License-File") == project["license-files"], meta.metadata.get_all("License-File")
    licences = [f for f in meta.files if ".dist-info/licenses/" in str(f)]
    carried = {str(f).split(".dist-info/licenses/", 1)[1] for f in licences}
    assert carried == set(project["license-files"]), carried
    empty = [str(f) for f in licences if not f.locate().is_file() or f.locate().stat().st_size == 0]
    assert not empty, f"notices missing or empty: {empty}"
    readme = (pathlib.Path(sys.argv[1]).parent / project["readme"]).read_text(encoding="utf-8")
    assert meta.metadata["Description-Content-Type"] == "text/markdown", meta.metadata["Description-Content-Type"]
    assert meta.metadata.get_payload() == readme, "the description isn't the package's README"
    dotnet = next(f for f in licences if str(f).endswith("dotnet/LICENSE.TXT")).locate().read_bytes()
    assert dotnet.lstrip().startswith(b"The MIT License") and b".NET Foundation" in dotnet, "dotnet/LICENSE.TXT isn't .NET's MIT licence"
    print(f"the wheel: nanoql {meta.version} from {nanoql.__file__}; a 5Y swap's fixed rate {rate!r}; 0 live handles; "
          f"{len(carried)} notices files, .NET's the MIT licence, the expression naming {project['license'].count(' AND ') + 1} licences; "
          f"the description the README, {len(readme):,} characters")
    return 0


if __name__ == "__main__":
    sys.exit(main())
