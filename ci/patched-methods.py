#!/usr/bin/env python3
"""Copy every game method ValheimTune patches out of an ilspycmd -p decompile.

  patched-methods.py <decompile-dir> <out-dir>

Targets come from the [HarmonyPatch(typeof(T), nameof(T.M))] / "M" attributes in
ValheimTune/Patches. Each target lands in <out-dir>/T.M.cs (all overloads, in source
order), so two builds' outputs can be compared with a plain diff -r.
Exits 1 if a target can't be found: a rename is exactly what this is here to catch.
"""
import pathlib, re, sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
ATTR = re.compile(r'HarmonyPatch\(typeof\((\w+)\),\s*(?:nameof\(\w+\.(\w+)\)|"(\w+)")')


def targets():
    found = set()
    for f in (ROOT / "ValheimTune" / "Patches").glob("*.cs"):
        for m in ATTR.finditer(f.read_text()):
            found.add((m.group(1), m.group(2) or m.group(3)))
    return sorted(found)


def block(lines, start):
    """Text of the member starting at lines[start]: up to its matching brace, or ';' if expression-bodied."""
    depth, out, opened = 0, [], False
    for line in lines[start:]:
        out.append(line)
        if not opened and "=>" in line and "{" not in line.split("=>")[0]:
            if line.rstrip().endswith(";"):
                return out
            continue
        depth += line.count("{") - line.count("}")
        opened = opened or "{" in line
        if opened and depth <= 0:
            return out
        if not opened and line.rstrip().endswith(";"):
            return out
    return out


def extract(src, member):
    lines = src.splitlines()
    head = re.compile(r'^\s*(?:\[.*\]\s*)?(?:(?:public|private|protected|internal|static|virtual|override|unsafe|extern|readonly|new)\s+)+[\w<>\[\],. ?]+\s' + re.escape(member) + r'\s*(\(|\{|$)')
    out = []
    for i, line in enumerate(lines):
        if head.match(line):
            out += block(lines, i) + [""]
    return "\n".join(out)


def main():
    src_dir, out_dir = map(pathlib.Path, sys.argv[1:3])
    out_dir.mkdir(parents=True, exist_ok=True)
    missing = []
    for typ, member in targets():
        files = list(src_dir.rglob(f"{typ}.cs"))
        text = extract(files[0].read_text(), member) if files else ""
        if not text.strip():
            missing.append(f"{typ}.{member}")
            continue
        (out_dir / f"{typ}.{member}.cs").write_text(text)
        print(f"{typ}.{member}: {text.count(chr(10))} lines")
    if missing:
        print("NOT FOUND in this build: " + ", ".join(missing))
        sys.exit(1)


if __name__ == "__main__":
    main()
