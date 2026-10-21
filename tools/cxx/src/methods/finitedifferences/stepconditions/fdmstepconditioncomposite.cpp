// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/methods/finitedifferences/stepconditions/fdmstepconditioncomposite.hpp, .cpp: conditions(), which the wheel
// binds but can't return (its list of conditions aborts the interpreter). "FdmStepConditionComposite::conditions"
// takes (stoppingTimes, snapshots, join): the composite of snapshot conditions at the given times under the stopping
// times (a list of lists of reals), or, with join a real, joinConditions of a snapshot at join and that composite;
// it returns {"stoppingTimes", "conditions"}, each condition ["snapshot", its time] or ["composite", its number of
// conditions].

#include "convert.hpp"

#include <ql/methods/finitedifferences/stepconditions/fdmsnapshotcondition.hpp>
#include <ql/methods/finitedifferences/stepconditions/fdmstepconditioncomposite.hpp>

#include <list>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"FdmStepConditionComposite::conditions",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 if (!a[0].is_array())
                     throw InvalidParams("expected lists of stopping times, got " + json::serialize(a[0]));
                 std::list<std::vector<Time>> stoppingTimes;
                 for (const json::value& times : a[0].get_array())
                     stoppingTimes.push_back(reals(times));
                 std::vector<Real> snapshots = reals(a[1]);
                 Real join = real(a[2]);
                 return [=] {
                     FdmStepConditionComposite::Conditions conditions;
                     for (Real t : snapshots)
                         conditions.push_back(ext::make_shared<FdmSnapshotCondition>(t));
                     auto composite = ext::make_shared<FdmStepConditionComposite>(stoppingTimes, conditions);
                     if (join != Null<Real>())
                         composite = FdmStepConditionComposite::joinConditions(
                             ext::make_shared<FdmSnapshotCondition>(join), composite);
                     json::array times, out;
                     for (Time t : composite->stoppingTimes())
                         times.emplace_back(t);
                     for (const auto& c : composite->conditions()) {
                         if (auto s = ext::dynamic_pointer_cast<FdmSnapshotCondition>(c))
                             out.emplace_back(json::array{"snapshot", s->getTime()});
                         else if (auto k = ext::dynamic_pointer_cast<FdmStepConditionComposite>(c))
                             out.emplace_back(
                                 json::array{"composite", static_cast<std::int64_t>(k->conditions().size())});
                         else
                             throw InvalidParams("a condition of another type");
                     }
                     return json::value(json::object{{"stoppingTimes", times}, {"conditions", out}});
                 };
             }},
        });

    }

}
