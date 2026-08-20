# Token Budget Policy

**Task:** AI01-008

Each orchestrated PR must define a token budget before implementation starts.

## Budget Fields

- Total budget
- Implementation budget
- Review budget
- Remediation budget
- Soft stop percentage
- Hard stop percentage
- Remediation loop limit

## MVP Defaults

- Remediation loop limit: 2
- Soft stop: 80%
- Hard stop: 100%

## Behavior

At soft stop, the Router reports budget pressure and avoids non-essential work.

At hard stop, the Router stops and requests Product Owner direction.

The Router must stop if completing the task inside budget would require reducing review quality, skipping CI, skipping Required Independent Review, or expanding scope.
