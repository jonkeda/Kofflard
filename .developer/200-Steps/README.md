# Steps

One file per step, named `<slice>.<nr>-<name>.md` (`0.1-the-first-step.md`). A step is small
enough for one sitting of an agent. A file without **The check** is refused.

## Template

```markdown
# <slice>.<nr> <name>

| | |
| --- | --- |
| Slice | <number and name> |
| Date | <yyyy-MM-dd> |
| State | Open, Green, or Split into <steps> |

## What

One or two sentences: what is true after this step that was not before.

## The check

The scenario or test that says so, by name and file. Written first, seen red.

## The planted fault

What is broken on purpose to see the check go red, and that it went red.

## The result

The commit, what changed, and anything left over (as friction rows or a next step).
```
