# Task 4 evidence

## Scope

Implemented sign-in and password recovery using local contract-shaped adapters. Auth artwork remains the existing image placeholder. No shared controls or backend files were changed.

## TDD evidence

- RED: `npm run test:unit -- src/features/sign-in/SignInForm.test.js src/features/reset-password/ResetPasswordForm.test.js` failed because both requested components were absent.
- GREEN: the same feature tests pass after implementing the forms and adapters.
- Full verification: `npm run test:unit` — 9 files / 37 tests passed.
- `npm run lint` — passed.
- `npm run build` — passed.

## Covered behavior

- Invalid sign-in credentials show an error message.
- Unconfirmed sign-in routes to `verify-email` while preserving the email query.
- Reset request routes to the code step with the email preserved.
- Reset code step starts a 55-second resend timer and decrements it each second.
- Local adapters expose success, validation, unconfirmed, invalid-credentials, and server-error outcomes.

## Important review fixes

- RED: `npm run test:unit -- src/features/sign-in/SignInForm.test.js src/shared/api/local-adapters.test.js` failed with 8 failing assertions: malformed reset emails and policy-invalid reset passwords were accepted, and malformed sign-in email reached the invalid-credentials path.
- GREEN: the same focused command passes with 2 files / 11 tests after adding shared email validation, enforcing the reset password policy in local adapters, and validating SignInForm email before submission.
