// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/cashflows/replication.hpp, which the wheel doesn't bind: DigitalReplication built from none, one or both of its
// arguments, the rest QuantLib's defaults, answered as {"replicationType", "gap"}, its two inspectors; and the stream
// text of a Replication::Type.

#include "convert.hpp"

#include <ql/cashflows/replication.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        json::value inspected(const DigitalReplication& r) {
            return json::object{{"replicationType", static_cast<int>(r.replicationType())}, {"gap", r.gap()}};
        }

        const Register unit({
            // DigitalReplication([t[, gap]])
            {"DigitalReplication",
             [](const Args& a) -> Call {
                 if (a.size() > 2)
                     arity(a, 2);
                 if (a.empty())
                     return [] { return inspected(DigitalReplication()); };
                 auto t = enumeration<Replication::Type>(a[0]);
                 if (a.size() == 1)
                     return [=] { return inspected(DigitalReplication(t)); };
                 Real gap = real(a[1]);
                 return [=] { return inspected(DigitalReplication(t, gap)); };
             }},
            {"operator<<(Replication::Type)",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 auto t = enumeration<Replication::Type>(a[0]);
                 return [=] { return json::value(streamed(t)); };
             }},
        });

    }

}
