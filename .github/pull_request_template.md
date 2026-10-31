<!-- What the change does, and why. Link the issue it closes, if any. -->

- [ ] The core still mirrors QuantLib: the change ports an upstream QuantLib change, or fixes a defect with a test
      that checks the fix against QuantLib. Anything QuantLib lacks is in the extensions layer, with its own tests.
- [ ] The tests pass, and any vector file the change adds or changes is written by its generator under
      `tools/vectors/gen/`.
- [ ] Every commit is signed off (`git commit -s`), certifying the Developer Certificate of Origin.
