// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/models/equity/batesmodel.hpp, .cpp: the det-jump and double-exponential Bates models, which the wheel doesn't
// expose. "BatesModel::read" takes (type, process, arguments, newParams): type "BatesDetJumpModel" (on a Bates process,
// arguments kappaLambda and thetaLambda), "BatesDoubleExpModel" (on a Heston process, arguments lambda, nuUp, nuDown
// and p) or "BatesDoubleExpDetJumpModel" (those, then kappaLambda and thetaLambda), the arguments a prefix of the
// constructor's, QuantLib's defaults for the rest; the process models/equity/hestonspec.hpp's spec. It returns the
// model's {"params"} and each accessor's value, the Heston model's five and the model's own; and, for newParams not
// null, the same after setParams(newParams), each name prefixed "after", with the "afterProcess" generateArguments
// built: {"type": "BatesProcess" or "HestonProcess", "v0", "kappa", "theta", "sigma", "rho"}, and a Bates process's
// "lambda", "nu" and "delta".

#include "convert.hpp"
#include "models/equity/hestonspec.hpp"

#include <ql/math/array.hpp>
#include <ql/models/equity/batesmodel.hpp>

#include <cctype>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        json::value array(const Array& a) {
            json::array out;
            for (Real x : a)
                out.push_back(json::value(x));
            return json::value(out);
        }

        Real argument(const std::vector<Real>& a, std::size_t i, Real fallback) {
            return i < a.size() ? a[i] : fallback;
        }

        ext::shared_ptr<HestonModel> makeModel(const std::string& type, const equity::ProcessSpec& p,
                                               const std::vector<Real>& a) {
            if (type == "BatesDetJumpModel")
                return ext::make_shared<BatesDetJumpModel>(equity::makeBatesProcess(p), argument(a, 0, 1.0),
                                                           argument(a, 1, 0.1));
            if (type == "BatesDoubleExpModel")
                return ext::make_shared<BatesDoubleExpModel>(equity::makeProcess(p), argument(a, 0, 0.1),
                                                             argument(a, 1, 0.1), argument(a, 2, 0.1),
                                                             argument(a, 3, 0.5));
            return ext::make_shared<BatesDoubleExpDetJumpModel>(
                equity::makeProcess(p), argument(a, 0, 0.1), argument(a, 1, 0.1), argument(a, 2, 0.1),
                argument(a, 3, 0.5), argument(a, 4, 1.0), argument(a, 5, 0.1));
        }

        void read(json::object& out, const std::string& prefix, const ext::shared_ptr<HestonModel>& m) {
            auto name = [&prefix](const std::string& n) {
                return prefix.empty() ? n : prefix + static_cast<char>(std::toupper(n[0])) + n.substr(1);
            };
            out[name("params")] = array(m->params());
            out[name("theta")] = m->theta();
            out[name("kappa")] = m->kappa();
            out[name("sigma")] = m->sigma();
            out[name("rho")] = m->rho();
            out[name("v0")] = m->v0();
            if (auto d = ext::dynamic_pointer_cast<BatesDetJumpModel>(m)) {
                out[name("nu")] = d->nu();
                out[name("delta")] = d->delta();
                out[name("lambda")] = d->lambda();
                out[name("kappaLambda")] = d->kappaLambda();
                out[name("thetaLambda")] = d->thetaLambda();
            } else if (auto e = ext::dynamic_pointer_cast<BatesDoubleExpModel>(m)) {
                out[name("p")] = e->p();
                out[name("nuDown")] = e->nuDown();
                out[name("nuUp")] = e->nuUp();
                out[name("lambda")] = e->lambda();
                if (auto f = ext::dynamic_pointer_cast<BatesDoubleExpDetJumpModel>(m)) {
                    out[name("kappaLambda")] = f->kappaLambda();
                    out[name("thetaLambda")] = f->thetaLambda();
                }
            }
        }

        json::value processResult(const ext::shared_ptr<HestonProcess>& p) {
            json::object out;
            auto bates = ext::dynamic_pointer_cast<BatesProcess>(p);
            out["type"] = bates ? "BatesProcess" : "HestonProcess";
            out["v0"] = p->v0();
            out["kappa"] = p->kappa();
            out["theta"] = p->theta();
            out["sigma"] = p->sigma();
            out["rho"] = p->rho();
            if (bates) {
                out["lambda"] = bates->lambda();
                out["nu"] = bates->nu();
                out["delta"] = bates->delta();
            }
            return json::value(out);
        }

        const Register unit({
            {"BatesModel::read",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 std::string type = text(a[0]);
                 if (type != "BatesDetJumpModel" && type != "BatesDoubleExpModel" &&
                     type != "BatesDoubleExpDetJumpModel")
                     throw InvalidParams("unknown model " + type);
                 equity::ProcessSpec p = equity::process(a[1]);
                 std::vector<Real> arguments = reals(a[2]);
                 if (arguments.size() > (type == "BatesDetJumpModel" ? 2U : type == "BatesDoubleExpModel" ? 4U : 6U))
                     throw InvalidParams("too many arguments for " + type);
                 bool reset = !a[3].is_null();
                 std::vector<Real> newParams = reset ? reals(a[3]) : std::vector<Real>();
                 return [=] {
                     ext::shared_ptr<HestonModel> m = makeModel(type, p, arguments);
                     json::object out;
                     read(out, "", m);
                     if (reset) {
                         Array x(newParams.begin(), newParams.end());
                         m->setParams(x);
                         read(out, "after", m);
                         out["afterProcess"] = processResult(m->process());
                     }
                     return json::value(out);
                 };
             }},
        });

    }

}
