#!/usr/bin/env python3
"""Print a branch's buildid from `steamcmd +app_info_print 896660` output, or "unknown".

  steam-buildid.py <app_info.txt> <branch>
"""
import re, sys

text = open(sys.argv[1], errors="replace").read()
branch = sys.argv[2]
m = re.search(r'"branches"\s*\{.*?"' + re.escape(branch) + r'"\s*\{[^{}]*?"buildid"\s*"(\d+)"', text, re.S)
print(m.group(1) if m else "unknown")
