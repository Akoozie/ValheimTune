#!/usr/bin/env python3
"""Retarget CompatTests.cs from the previous newest build to a newly ported one.

  compat-tests-bump.py <CompatTests.cs> <old-game> <new-game> <old-gate-list>
"""
import sys

path, old, new, builds = sys.argv[1:]
lines = open(path).read().split("\n")
known = lambda v: f'Assert.True(Compat.IsKnown("{v}", Compat.DefaultKnownGoodBuilds));'


def find(text):
    return next(i for i, l in enumerate(lines) if text in l)


def indent(line):
    return line[: len(line) - len(line.lstrip())]


# The ported-build test moves to the new build.
i = find("ShippedDefaultCoversThePortedBuild")
lines[i + 1] = lines[i + 1].replace(known(old), known(new))
assert known(new) in lines[i + 1], "ShippedDefaultCoversThePortedBuild not in the expected shape"

# The build that was newest heads the "previous builds" list.
i = find("ShippedDefaultStillCoversThePreviousBuilds") + 2
lines.insert(i, indent(lines[i]) + known(old))

# A config from the previous release must still get the new build.
i = find("StaleConfigStillGetsTheShippedBuild") + 2
while lines[i].strip() != "}":
    i += 1
lines.insert(i, indent(lines[i - 1]) + f'Assert.True(Compat.IsKnownOrShipped("{new}", "{builds}"));')

open(path, "w").write("\n".join(lines))
