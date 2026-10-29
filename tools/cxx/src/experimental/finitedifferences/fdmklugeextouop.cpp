// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/finitedifferences/fdmextendedornsteinuhlenbeckop.hpp, fdmextoujumpop.hpp and fdmklugeextouop.hpp
// (.cpp each), which the wheel doesn't expose, on meshers built as QuantLib's engines build them:
//
//     FdmExtendedOrnsteinUhlenbeckOp::values  (process, xGrid, maturity, rate, times, s, r)
//     FdmExtOUJumpOp::values                  (jumps, [xGrid, yGrid], maturity, rate, order, times, s, r)
//     FdmKlugeExtOUOp::values                 (klugeOU, [xGrid, yGrid, uGrid], maturity, rate, order, times, s, r)
//
// The processes are extouspec.hpp's specs; the meshers FdmSimpleProcess1dMesher on each OU process to the maturity and
// ExponentialJump1dMesher on the jumps; the curve flat at the rate from the evaluation date on Actual/365 (Fixed), no
// boundary conditions, the jump integral's Gauss-Laguerre order as given. For each [t1, t2] of times, after
// setTime(t1, t2): {"apply", "applyMixed", "applyDirection", "solveSplitting", "preconditioner"}, each a list per time,
// the direction ones a list per direction, solve_splitting and preconditioner at s, all on r, which has one value per
// mesher point.

#include "experimental/processes/extouspec.hpp"

#include <ql/experimental/finitedifferences/fdmextendedornsteinuhlenbeckop.hpp>
#include <ql/experimental/finitedifferences/fdmextoujumpop.hpp>
#include <ql/experimental/finitedifferences/fdmklugeextouop.hpp>
#include <ql/methods/finitedifferences/meshers/exponentialjump1dmesher.hpp>
#include <ql/methods/finitedifferences/meshers/fdmmeshercomposite.hpp>
#include <ql/methods/finitedifferences/meshers/fdmsimpleprocess1dmesher.hpp>
#include <ql/methods/finitedifferences/operators/fdmlinearoplayout.hpp>
#include <ql/settings.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

#include <functional>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        json::array array(const Array& x) {
            json::array out;
            for (Real xi : x)
                out.emplace_back(xi);
            return out;
        }

        std::vector<std::vector<Real>> timesOf(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected times [t1, t2], got " + json::serialize(v));
            std::vector<std::vector<Real>> out;
            for (const json::value& t : v.get_array()) {
                out.push_back(reals(t));
                if (out.back().size() != 2)
                    throw InvalidParams("a time is [t1, t2]");
            }
            return out;
        }

        ext::shared_ptr<YieldTermStructure> curve(Real rate) {
            return ext::make_shared<FlatForward>(Settings::instance().evaluationDate(), rate, Actual365Fixed());
        }

        json::value values(FdmLinearOpComposite& op, const std::vector<std::vector<Real>>& times, Real s,
                           const std::vector<Real>& rv) {
            const Array r(rv.begin(), rv.end());
            json::array apply, mixed, direction, splitting, preconditioner;
            for (const auto& t : times) {
                op.setTime(t[0], t[1]);
                apply.emplace_back(array(op.apply(r)));
                mixed.emplace_back(array(op.apply_mixed(r)));
                json::array d, sp;
                for (Size i = 0; i < op.size(); ++i) {
                    d.emplace_back(array(op.apply_direction(i, r)));
                    sp.emplace_back(array(op.solve_splitting(i, r, s)));
                }
                direction.emplace_back(d);
                splitting.emplace_back(sp);
                preconditioner.emplace_back(array(op.preconditioner(r, s)));
            }
            json::object out;
            out["apply"] = apply;
            out["applyMixed"] = mixed;
            out["applyDirection"] = direction;
            out["solveSplitting"] = splitting;
            out["preconditioner"] = preconditioner;
            return json::value(out);
        }

        void sized(const ext::shared_ptr<FdmMesher>& mesher, const std::vector<Real>& r) {
            if (r.size() != mesher->layout()->size())
                throw InvalidParams("r needs one value per mesher point");
        }

        const Register unit({
            {"FdmExtendedOrnsteinUhlenbeckOp::values",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 const json::value spec = a[0];
                 processes::extendedOU(spec);
                 const Integer xGrid = integer(a[1]);
                 const Real maturity = real(a[2]), rate = real(a[3]);
                 const auto times = timesOf(a[4]);
                 const Real s = real(a[5]);
                 const std::vector<Real> r = reals(a[6]);
                 return [=] {
                     const auto p = processes::extendedOU(spec);
                     const auto mesher = ext::make_shared<FdmMesherComposite>(
                         ext::make_shared<FdmSimpleProcess1dMesher>(xGrid, p, maturity));
                     sized(mesher, r);
                     FdmExtendedOrnsteinUhlenbeckOp op(mesher, p, curve(rate), FdmBoundaryConditionSet());
                     return values(op, times, s, r);
                 };
             }},
            {"FdmExtOUJumpOp::values",
             [](const Args& a) -> Call {
                 arity(a, 8);
                 const json::value spec = a[0];
                 processes::jumps(spec);
                 const std::vector<Integer> grids = integers(a[1]);
                 if (grids.size() != 2)
                     throw InvalidParams("the grids are [xGrid, yGrid]");
                 const Real maturity = real(a[2]), rate = real(a[3]);
                 const Integer order = integer(a[4]);
                 const auto times = timesOf(a[5]);
                 const Real s = real(a[6]);
                 const std::vector<Real> r = reals(a[7]);
                 return [=] {
                     const auto p = processes::jumps(spec);
                     const auto mesher = ext::make_shared<FdmMesherComposite>(
                         ext::make_shared<FdmSimpleProcess1dMesher>(grids[0], p->getExtendedOrnsteinUhlenbeckProcess(),
                                                                    maturity),
                         ext::make_shared<ExponentialJump1dMesher>(grids[1], p->beta(), p->jumpIntensity(), p->eta()));
                     sized(mesher, r);
                     FdmExtOUJumpOp op(mesher, p, curve(rate), FdmBoundaryConditionSet(), order);
                     return values(op, times, s, r);
                 };
             }},
            {"FdmKlugeExtOUOp::values",
             [](const Args& a) -> Call {
                 arity(a, 8);
                 const json::value spec = a[0];
                 processes::klugeOU(spec);
                 const std::vector<Integer> grids = integers(a[1]);
                 if (grids.size() != 3)
                     throw InvalidParams("the grids are [xGrid, yGrid, uGrid]");
                 const Real maturity = real(a[2]), rate = real(a[3]);
                 const Integer order = integer(a[4]);
                 const auto times = timesOf(a[5]);
                 const Real s = real(a[6]);
                 const std::vector<Real> r = reals(a[7]);
                 return [=] {
                     const auto p = processes::klugeOU(spec);
                     const auto kluge = p->getKlugeProcess();
                     const auto mesher = ext::make_shared<FdmMesherComposite>(
                         ext::make_shared<FdmSimpleProcess1dMesher>(
                             grids[0], kluge->getExtendedOrnsteinUhlenbeckProcess(), maturity),
                         ext::make_shared<ExponentialJump1dMesher>(grids[1], kluge->beta(), kluge->jumpIntensity(),
                                                                   kluge->eta()),
                         ext::make_shared<FdmSimpleProcess1dMesher>(grids[2], p->getExtOUProcess(), maturity));
                     sized(mesher, r);
                     FdmKlugeExtOUOp op(mesher, p, curve(rate), FdmBoundaryConditionSet(), order);
                     return values(op, times, s, r);
                 };
             }},
        });

    }

}
