#!/usr/bin/env python3
"""Б2: source contracts only, NOT a semantic build or combat execution.
Checks typed MoveState → Hit/Blast adapters, declared entourage, shipped individual roles and generated rows.
"""
from __future__ import annotations
import csv
import importlib.util
import io
import re
from pathlib import Path
from tree_sitter import Language, Parser
import tree_sitter_c_sharp

ROOT = Path(__file__).resolve().parents[2]
CODE = ROOT / "mod/SlayTheRequiemCode"
PARSER = Parser(Language(tree_sitter_c_sharp.language()))


def walk(node):
    yield node
    for child in node.named_children:
        yield from walk(child)


def classes(root=CODE / "Monsters"):
    result = {}
    for path in sorted(root.glob("*.cs")):
        data = path.read_bytes()
        for node in walk(PARSER.parse(data).root_node):
            if node.type != "class_declaration":
                continue
            name = node.child_by_field_name("name").text.decode()
            body = node.child_by_field_name("body")
            methods = {}
            for member in body.named_children:
                if member.type == "method_declaration":
                    methods[member.child_by_field_name("name").text.decode()] = member.text.decode()
            result[name] = (path.relative_to(ROOT).as_posix(), body, methods)
    return result


def inspect_moves(body, methods):
    for node in walk(body):
        if node.type != "object_creation_expression":
            continue
        typ = node.child_by_field_name("type")
        if typ is None or typ.text != b"MoveState":
            continue
        args = next(c for c in node.named_children if c.type == "argument_list").named_children
        if len(args) < 2:
            continue
        ids = re.findall(r'"([A-Z][A-Z_0-9]+)"', args[0].text.decode())
        handler = args[1].text.decode()
        for _ in range(3):
            calls = set(re.findall(r"\b(\w+)\s*\(", handler)) | {handler.strip()}
            extra = "\n".join(methods[n] for n in sorted(calls) if n in methods and methods[n] not in handler)
            if not extra:
                break
            handler += "\n" + extra
        yield ids, handler


def check():
    errors = []
    module_path = ROOT / "tools/markup/build_monsters.py"
    spec = importlib.util.spec_from_file_location("combat_monsters", module_path)
    markup = importlib.util.module_from_spec(spec); spec.loader.exec_module(markup)
    markup.validate_roles()
    generated = io.StringIO()
    generated.write("# Генерируется tools/markup/build_monsters.py — не править руками.\n")
    writer = csv.DictWriter(generated, fieldnames=markup.FIELDS, lineterminator="\n"); writer.writeheader()
    for key, (fac, plating, poise, atk, note) in sorted({**markup.M, **markup.MOD}.items()):
        res = markup.FACTIONS[fac]
        writer.writerow(dict(key=key, faction=fac, plating=plating, poise_bonus=poise, res_fire=res[0], res_frost=res[1],
                             res_shock=res[2], res_poison=res[3], res_magic=res[4], attack_type=atk, role=markup.role_of(key), note=note))
    if generated.getvalue() != (ROOT / "mod/SlayTheRequiem/data/monsters_requiem.csv").read_text():
        errors.append("monster table stale: run python tools/markup/build_monsters.py")
    static = {}
    for typ, moves in re.findall(r'Add\(DamageType\.(\w+), "([A-Z_0-9 ]+)"\)', (CODE / "Core/MonsterMoveRules.cs").read_text()):
        for move in moves.split():
            if move in static: errors.append(f"duplicate move type: {move}")
            static[move] = typ
    count = 0
    inventory = classes()
    for name, (path, body, methods) in inventory.items():
        source = body.text.decode()
        if name not in markup.MOD:
            continue
        dynamic = "override DamageType CurrentAttackType" in source
        for ids, handler in inspect_moves(body, methods):
            # Actual explicitly typed hits, not arbitrary DamageType used by a status/resistance condition.
            types = set(re.findall(r'\b(?:Hit|HitOne|HitAndDrain|Blast)\([^;\n]*?DamageType\.(\w+)', handler))
            if not types or dynamic:
                continue
            if len(types) != 1:
                errors.append(f"{name}: multitype move needs explicit dynamic intent")
                continue
            typ = next(iter(types))
            for move in ids:
                expected = static.get(re.sub(r"_[23]$", "", move), "Physical")
                count += 1
                if typ != expected: errors.append(f"{name}.{move}: hit={typ}, intent/snapshot={expected}")
        summoned = set(re.findall(r"\bawait Summon<(\w+)>\(", source))
        declared = set(re.findall(r"ModelDb.Monster<(\w+)>\(\)", source.split("SummonModels =>", 1)[1].split(";", 1)[0])) if "SummonModels =>" in source else set()
        if summoned != declared:
            errors.append(f"{name}: undeclared/stale entourage {sorted(summoned ^ declared)} ({path})")
    return count, len(markup.M) + len(markup.MOD), errors


def main():
    moves, roles, errors = check()
    for error in errors: print("ERROR:", error)
    print(f"Combat source contracts: {moves} statically typed moves, {roles} role rows, {len(errors)} errors (not runtime traces)")
    return bool(errors)


if __name__ == "__main__":
    raise SystemExit(main())
