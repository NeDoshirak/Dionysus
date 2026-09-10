# Task 5 report

- Figma audit: reviewed nodes `6:479` (projects listing) and `6:1767` (search/list states) from the supplied file.
- Contract: project summaries use exactly `id`, `name`, `createdAt`, and `status`; local search refuses queries shorter than two characters.
- TDD red: targeted tests initially failed because the new components did not exist.
- TDD green: targeted tests pass (`2` files, `4` tests).
- Scope: added project entity API/model, app header, project list, project search, and projects page. Router and local adapters were intentionally not modified.
