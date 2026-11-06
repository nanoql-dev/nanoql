// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// A user's first program over nanoql.hpp (docs/cpp.md), built with no option beyond C++17: a flat curve's discount, a
// refusal taken as an exception, every handle released, and the library's ABI the header's.
#include <cstdio>
#include <exception>

#include "nanoql.hpp"

using namespace nanoql;

int main()
{
    int failures = 0;
    {
        FlatForward curve(Date(15, Month::March, 2023), 0.03, ConventionSet::quantLib().dayCounter("Actual365Fixed"));
        double d = curve.discount(1.0);
        std::printf("discount(1) = %.17g\n", d);
        if (!(d > 0.9704 && d < 0.9705)) {
            failures++;
        }
        try {
            curve.discount(-1.0);
            std::printf("no refusal for a negative time\n");
            failures++;
        } catch (const std::exception& e) {
            std::printf("refused: %s\n", e.what());
        }
    }
    std::printf("live handles: %lld; ABI %d, the header's %d\n", static_cast<long long>(liveHandles()),
                static_cast<int>(nql_abi_version()), NQL_ABI_VERSION);
    if (liveHandles() != 0 || nql_abi_version() != NQL_ABI_VERSION) {
        failures++;
    }
    std::printf("consumer: %s\n", failures == 0 ? "passed" : "FAILED");
    return failures == 0 ? 0 : 1;
}
