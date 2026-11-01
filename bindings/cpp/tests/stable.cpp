// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The headers as a user includes them by default, without NANOQL_EXPERIMENTAL (docs/bindings.md, "C and C++"): nanoql's
// header and the test types' over it compile with the experimental API left out, and their stable classes run. Built
// beside conformance.cpp, apart from it, since the opt-in changes the classes' definitions.
#ifdef NANOQL_EXPERIMENTAL
#error "stable.cpp checks the headers without NANOQL_EXPERIMENTAL"
#endif

#include <cstdio>
#include <vector>

#include "nqlt.hpp"

using namespace nanoql;

int main()
{
    int failures = 0;
    {
        FlatForward curve(Date(15, Month::March, 2023), 0.03, ConventionSet::quantLib().dayCounter("Actual365Fixed"));
        double d = curve.discount(1.0);
        if (!(d > 0.9704 && d < 0.9705)) {
            std::printf("FAILED: a stable class of nanoql's: discount(1) = %.17g\n", d);
            failures++;
        }
        if (nqlt::Echo::total(std::vector<double>{1.0, 2.0, 3.5}) != 6.5) {
            std::printf("FAILED: a stable member of the test types\n");
            failures++;
        }
    }
    if (liveHandles() != 0) {
        std::printf("FAILED: %lld live handles at the end\n", static_cast<long long>(liveHandles()));
        failures++;
    }
    std::printf("stable: %s\n", failures == 0 ? "passed" : "FAILED");
    return failures == 0 ? 0 : 1;
}
