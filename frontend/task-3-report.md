# Task 3 evidence

- Sign-up and confirmation-success content uses a responsive 381px inner width.
- Verification uses a responsive 420px inner width.
- Verification email is masked in the UI (for example, `person@example.com` displays as `p***@example.com`) while the original prop remains available for verification.
- OTP cells move focus forward after a digit, move backward on Backspace from an empty cell, and distribute numeric digits from paste input.
- Auth pages retain the existing `--color-accent-strong` value of `#d92d17`; no SVG or network integration was added.

Verification:

```text
npm run test:unit -- src/features/verify-email/VerifyEmailForm.test.js src/features/sign-up/SignUpForm.test.js
Test Files  2 passed (2)
Tests  6 passed (6)
```
