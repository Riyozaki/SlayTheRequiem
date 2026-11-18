#!/usr/bin/env python3
"""Find duplicate C# type declarations in each real project (including Compile links).

Tree-sitter syntax alone accepts two identical non-partial patch classes. This gate catches that source
error WITHOUT pretending to compile against the game. Partial types, nesting, generic arity, file-local
types and separate assemblies are kept distinct. It is not a replacement for C# semantic compilation.
"""
from __future__ import annotations

from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path
import fnmatch
import re
import sys
import xml.etree.ElementTree as ET

from tree_sitter import Language, Parser
import tree_sitter_c_sharp as cs

ROOT = Path(__file__).resolve().parents[2]
SKIP = {"bin", "obj", ".godot", ".git", ".arena", ".arena-ci", "node_modules"}
KINDS = {"class_declaration", "struct_declaration", "interface_declaration", "record_declaration",
         "enum_declaration", "delegate_declaration"}


@dataclass(frozen=True)
class Declaration:
    name: str
    kind: str
    partial: bool
    path: str
    line: int


def declarations(text: bytes, path: str) -> list[Declaration]:
    tree = Parser(Language(cs.language())).parse(text)
    if tree.root_node.has_error:
        raise ValueError(f"{path}: C# parse error; run check_csharp_syntax.py")
    result = []

    def walk(node, namespace=(), parents=(), local=""):
        for child in node.named_children:
            if child.type == "file_scoped_namespace_declaration":
                namespace += (child.child_by_field_name("name").text.decode(),)
            elif child.type == "namespace_declaration":
                walk(child.child_by_field_name("body"), namespace + (child.child_by_field_name("name").text.decode(),), parents, local)
            elif child.type in KINDS:
                modifiers = {item.text.decode() for item in child.named_children if item.type == "modifier"}
                file_local = local or (path if "file" in modifiers else "")
                parameters = next((item for item in child.named_children if item.type == "type_parameter_list"), None)
                arity = sum(item.type == "type_parameter" for item in parameters.named_children) if parameters else 0
                name = child.child_by_field_name("name").text.decode().lstrip("@") + (f"`{arity}" if arity else "")
                qualified = ".".join((*namespace, *parents, name)) + (f" [file:{file_local}]" if file_local else "")
                # record class and record struct are not compatible partial declarations.
                kind = child.type
                if kind == "record_declaration":
                    kind += " struct" if any(item.type == "struct" for item in child.children) else " class"
                result.append(Declaration(qualified, kind, "partial" in modifiers, path, child.start_point.row + 1))
                body = child.child_by_field_name("body")
                if body is not None:
                    walk(body, namespace, parents + (name,), file_local)
            elif child.type.startswith("preproc_"):
                # No conditional duplicate declarations currently exist in this project. Do not silently omit a branch.
                walk(child, namespace, parents, local)

    walk(tree.root_node)
    return result


def conflicts(sources: dict[str, bytes]) -> tuple[list[str], int]:
    indexed = defaultdict(list)
    for path, data in sorted(sources.items()):
        for row in declarations(data, path):
            indexed[row.name].append(row)
    errors = []
    for name, rows in sorted(indexed.items()):
        if len(rows) < 2:
            continue
        if all(row.partial for row in rows) and len({row.kind for row in rows}) == 1:
            continue
        locations = ", ".join(f"{row.path}:{row.line}" for row in rows)
        errors.append(f"Duplicate/incompatible non-partial type {name}: {locations}")
    return errors, sum(map(len, indexed.values()))


def project_sources(project: Path) -> list[Path]:
    """The repository uses implicit .cs items plus literal/glob Compile Include/Remove; no evaluated MSBuild claims."""
    xml = ET.parse(project).getroot()
    base = project.parent
    paths = {path.resolve() for path in base.rglob("*.cs") if not set(path.relative_to(base).parts) & SKIP}
    if any(node.text == "false" for node in xml.findall(".//EnableDefaultCompileItems")):
        paths.clear()
    for node in xml.findall(".//Compile"):
        if "Include" in node.attrib:
            include = node.attrib["Include"].replace("\\", "/")
            if re.search(r"\$\(|%\(|;", include):
                raise ValueError(f"Unsupported Compile Include in {project.name}: {include}")
            matches = {path.resolve() for path in base.glob(include) if path.is_file()}
            if not matches:
                raise ValueError(f"Missing Compile input in {project.name}: {include}")
            paths |= matches
        if "Remove" in node.attrib:
            for pattern in node.attrib["Remove"].replace("\\", "/").split(";"):
                paths = {path for path in paths if not path.is_relative_to(base.resolve())
                         or not (fnmatch.fnmatchcase(path.relative_to(base.resolve()).as_posix(), pattern)
                                 or pattern.endswith("/**") and path.relative_to(base.resolve()).as_posix().startswith(pattern[:-2]))}
    return sorted(paths)


def check() -> tuple[list[str], list[str]]:
    errors, summary = [], []
    projects = sorted(path for parent in (ROOT / "mod", ROOT / "tools") for path in parent.rglob("*.csproj")
                      if not set(path.relative_to(ROOT).parts) & SKIP)
    for project in projects:
        inputs = project_sources(project)
        sources = {path.relative_to(ROOT).as_posix(): path.read_bytes() for path in inputs}
        duplicates, count = conflicts(sources)
        name = project.relative_to(ROOT).as_posix()
        errors.extend(f"{name}: {error}" for error in duplicates)
        summary.append(f"{name}: {len(inputs)} source files, {count} type declarations, {len(duplicates)} duplicate errors")
    return errors, summary


def main():
    try:
        errors, summary = check()
        print("\n".join(summary + errors))
        print(f"Declaration source gate: {len(errors)} errors; NOT full mod semantic compilation")
        return bool(errors)
    except (ValueError, OSError, ET.ParseError) as error:
        print("ERROR:", error)
        return 1


if __name__ == "__main__":
    sys.exit(main())
