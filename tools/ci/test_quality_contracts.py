"""B3 source/asset regressions. These are NOT semantic full-mod, Godot, audio-mix or playtest acceptance."""
from __future__ import annotations

import hashlib
import importlib.util
from pathlib import Path
import re
import sys
import unittest

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
CODE = ROOT / "mod/SlayTheRequiemCode"


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, ROOT / path)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


icons = load("str_readable_icons", "tools/art/readable_icons.py")
illustrations = load("str_illustrations", "tools/art/cards/illustrations.py")
sprites = load("str_illustrated_sprites", "tools/art/monsters/illustrated_sprites.py")
assets = load("str_quality_asset_manifest", "tools/assets/asset_manifest.py")


def source(path):
    return (CODE / path).read_text(encoding="utf-8-sig")


class QualitySourceContracts(unittest.TestCase):
    def test_readable_sources_receipts_and_alpha_outlines_match(self):
        self.assertEqual(0, icons.check())
        self.assertEqual(20, len(icons.MAP))
        self.assertEqual(14, len(icons.CAMP))

    def test_every_camp_option_uses_the_complete_runtime_option_id(self):
        rows = [r for r in assets.build_rows() if r["kind"] == "rest_option"]
        self.assertEqual(14, len(rows))
        for row in rows:
            self.assertEqual("own", row["present"], row)
            self.assertTrue(row["path"].startswith("images/ui/rest_site/option_slaytherequiem_"), row)
        self.assertIn("OptionId.ToLowerInvariant()", source("Alchemy/CampOptions.cs"))

    def test_native_map_outline_coverage_is_complete(self):
        rows = [r for r in assets.build_rows() if r["path"].startswith("images/map/icons/")]
        self.assertEqual(40, len(rows))
        self.assertTrue(all(r["present"] == "own" for r in rows))
        code = source("UI/MapVisuals.cs")
        self.assertIn("outline.Texture = mask; outline.Visible = true", code)
        self.assertNotIn("outline.Visible = false", code)
        self.assertIn("RestoreNative(__instance)", code)
        self.assertIn("RememberNativeAndApply(__instance)", code)
        self.assertIn("map_unknown_outline.tres", code)

    def test_32px_shop_alpha_shapes_are_distinct_without_a_shared_counter(self):
        # A duplicate/clipped/empty-image prefilter, NOT a perceptual or in-game acceptance score.
        keys = ("armorer", "enchanter", "herbalist", "jeweler", "caravan", "fence")
        silhouettes = []
        for key in keys:
            with Image.open(icons.RES / f"map/icons/{key}.png") as im:
                alpha = im.convert("RGBA").resize((32, 32), Image.Resampling.LANCZOS).getchannel("A")
            mask = bytes(int(a >= 128) for a in alpha.tobytes())
            self.assertGreater(sum(mask), 100, key)
            self.assertLess(sum(mask), 900, key)
            silhouettes.append(mask)
        self.assertEqual(len(keys), len(set(silhouettes)))
        for i, a in enumerate(silhouettes):
            for j, b in enumerate(silhouettes[:i]):
                overlap = sum(x and y for x, y in zip(a, b)) / sum(x or y for x, y in zip(a, b))
                self.assertLess(overlap, .85, (keys[i], keys[j], overlap))

    def test_native_size_svg_does_not_reintroduce_hatching_or_external_images(self):
        for rel, svg, _ in icons.sources():
            self.assertNotRegex(svg, r"<(?:pattern|filter|image|text|script)\b", str(rel))
            self.assertNotIn("http:", svg.replace('xmlns="http://www.w3.org/2000/svg"', ""))
            for width in re.findall(r'stroke-width="([\d.]+)"', svg):
                self.assertTrue(float(width) == 0 or float(width) >= 4, (rel, width))

    def test_promoted_scenes_keep_masters_and_exact_small_and_big_pixels(self):
        self.assertEqual(0, illustrations.check())
        self.assertEqual({"whirlwind", "focused_aim", "firebolt", "shield_bash", "backstab", "arcane_focus"}, illustrations.promoted_keys())
        for row in illustrations.entries():
            self.assertIn("AI-generated", row["provenance"])
            self.assertIn("pending", row["acceptance"])
            self.assertEqual(row["master_sha256"], hashlib.sha256((ROOT / row["master"]).read_bytes()).hexdigest())

    def test_phase_and_form_exports_keep_ground_bounds_alpha_and_provenance(self):
        self.assertEqual(0, sprites.check())
        self.assertEqual(4, len(sprites.entries()))
        for row in sprites.entries():
            with Image.open(sprites.target(row)) as im:
                self.assertEqual("RGBA", im.mode)
                self.assertEqual(tuple(row["canvas"]), im.size)
                self.assertEqual(row["fit_box_xyxy"][3], im.getchannel("A").getbbox()[3])
        renderer = (ROOT / "tools/art/render_ui_icons.mjs").read_text()
        self.assertIn("sprite-manifest.json", renderer)
        self.assertIn("promoted.has(", renderer)

    def test_legacy_portrait_render_cannot_overwrite_a_promoted_master_export(self):
        path = ROOT / "tools/art/cards/draw_portraits.py"
        self.assertIn("PORTRAITS.keys() - PROMOTED", path.read_text())
        self.assertIn("illustrations.check()", path.read_text())

    def test_cache_is_wired_to_native_presence_and_display_amount_events(self):
        code = source("UI/NEnemyTraitChips.cs")
        for token in ("InvalidatedListView<PowerModel>", "PowerApplied += Observe", "PowerApplied -= Observe",
                      "PowerRemoved += Removed", "PowerRemoved -= Removed", "DisplayAmountChanged += Dirty",
                      "DisplayAmountChanged -= Dirty", "_view.Reset()", "_view.Refresh()"):
            self.assertIn(token, code)
        builder = code.split("private void BuildTraits", 1)[1].split("public override void _Process", 1)[0]
        self.assertNotIn("new List", builder)
        self.assertNotIn("ToArray", builder)
        process = code.split("public override void _Process(double delta)", 1)[1].split("private void ReportHeight", 1)[0]
        self.assertNotRegex(process, r"\b(?:Collect|Sort|Aggregate|GetHashCode)\s*\(")
        self.assertIn("if (!_paintDirty) return", process)
        self.assertIn("ReportHeight(0)", process)
        self.assertIn("string.Compare(a.Id.Entry, b.Id.Entry, StringComparison.Ordinal)", code)

    def test_disabling_compact_mode_reconciles_ui_not_model_powers(self):
        code = source("UI/CreatureResourceUiPatch.cs")
        patch = code.split("public static class CompactPowerRowSettingsPatch", 1)[1]
        self.assertIn("RequiemMotion.Watch", patch)
        self.assertIn("Remove(__instance, power)", patch)
        self.assertIn("Add(__instance, power)", patch)
        self.assertNotRegex(patch, r"\b(?:PowerCmd|ApplyPowerInternal|RemovePowerInternal|SetAmount)\b")
        self.assertNotIn("if (RequiemConfig.CompactEnemyStatuses)", code.split("public static class CompactPowerRowSettingsPatch", 1)[0])

    def test_reduced_motion_is_local_and_does_not_bump_saved_or_network_contracts(self):
        self.assertIn("public static bool ReducedMotion", source("Config/RequiemConfig.cs"))
        self.assertNotIn("ReducedMotion", source("Config/RunSettings.cs"))
        self.assertIn("TreeExiting += Exit", source("UI/RequiemMotion.cs"))
        self.assertIn("Changed -= update", source("UI/RequiemMotion.cs"))
        self.assertIn("presentation = new", source("Debugging/RunDiagnostics.cs"))

    def test_phase_sprite_is_committed_before_optional_animation_not_in_a_callback(self):
        swap = source("Monsters/OwnArtMonster.cs").split("public static bool Swap(NCreatureVisuals?", 1)[1]
        self.assertLess(swap.index("body.Texture = texture"), swap.index("!RequiemMotion.Enabled"))
        self.assertNotIn("TweenCallback", swap)
        self.assertIn("State(body).Dead", swap)
        self.assertIn('state.Dead && trigger != "Revive"', source("Monsters/OwnArtMonster.cs"))

    def test_decorative_loops_and_model_flashes_have_an_owner_scoped_motion_gate(self):
        for path in ("Equipment/PaperDoll.cs", "Transformations/HeroLooks.cs", "UI/RequiemVfx.cs"):
            self.assertIn("RequiemMotion.Decorate", source(path), path)
        self.assertIn("Config.RequiemConfig.CombatVfx && flash", source("Transformations/FormRelic.cs"))
        self.assertIn("RequiemMotion.Enabled && !TestMode.IsOn", source("UI/RequiemVfx.cs"))
        self.assertIn("ResetAction(pose)", source("Equipment/PaperDoll.cs"))

    def test_repeated_denial_and_container_entry_do_not_accumulate_position_drift(self):
        code = source("UI/RequiemKit.cs")
        self.assertIn("state.FinishEntry(control); state.FinishDenied(control)", code)
        self.assertIn("dy != 0 && control.GetParent() is not Container", code)
        toast = source("UI/NRequiemToast.cs")
        build, ready = toast.split("public override void _Ready()", 1)
        self.assertNotIn("CreateTween()", build)
        self.assertIn("life.TweenCallback(Callable.From(QueueFree))", ready)

    def test_shelf_cells_survive_timer_changes_and_never_mutate_gameplay(self):
        code = source("UI/NShoutShelfBar.cs")
        self.assertIn("Dictionary<CardModel, Slot>", code)
        self.assertIn("ReferenceEqualityComparer.Instance", code)
        self.assertIn("slot.Turns == entry.Turns", code)
        self.assertIn("Shouts.Capacity(owner)", code)
        self.assertIn('"UI.shelf.pending.title"', code)
        self.assertIn("PileType.Hand", code)
        self.assertIn("PileType.Discard", code)
        self.assertNotRegex(code, r"\b(?:CardPileCmd|PowerCmd|PrepareReturn|HasBeenRemovedFromState|AddGeneratedCardToCombat)\b")
        self.assertNotRegex(code, r"(?:RequiemVfx|RequiemSounds)\.Enabled")
        self.assertNotIn("GetChildren()", code)

    def test_named_overlays_detach_before_deferred_free_and_never_reuse_queued_nodes(self):
        helper = source("UI/RequiemNodes.cs")
        self.assertLess(helper.index("RemoveChild(child)"), helper.index("child.QueueFree()"))
        self.assertIn("!GodotObject.IsInstanceValid(child)", helper)
        self.assertIn("child.IsQueuedForDeletion()", helper)
        for path in ("Equipment/PaperDoll.cs", "Transformations/HeroLooks.cs"):
            code = source(path)
            self.assertIn("RequiemNodes.Find<", code)
            self.assertIn("RequiemNodes.Retire(existing)", code)
            self.assertNotIn("existing?.QueueFree()", code)
        self.assertIn("RequiemNodes.Clear(container)", source("Transformations/HeroLooks.cs"))

    def test_equipment_death_fades_descendants_and_refresh_keeps_dead_state(self):
        code = source("Equipment/PaperDoll.cs")
        self.assertEqual(1, code.count("public static class PaperDollDeathPatch"))
        self.assertIn('TweenProperty(part, "modulate:a"', code)
        self.assertNotIn('TweenProperty(part, "self_modulate:a"', code)
        self.assertIn("SetDead(holder, true, animate: false)", code)
        self.assertIn("SetDead(root, true, animate: false)", code)
        self.assertLess(code.index("life.Watching = true"), code.index("RequiemMotion.Watch(visuals"))
        config = source("Config/RequiemConfig.cs")
        setting = config.split("public static bool ShowPaperDoll", 1)[1].split("[ConfigEntry", 1)[0]
        self.assertIn("SettingsChanged()", setting)
        self.assertNotIn("ShowPaperDoll", source("Config/RunSettings.cs"))

    def test_control_exit_cleans_motion_pose_not_just_setting_toggles(self):
        code = source("UI/RequiemKit.cs")
        self.assertIn("owner.TreeExiting += Finish", code)
        self.assertIn("state.FinishEntry(owner); state.FinishDenied(owner)", code)
        self.assertIn("owner.Scale = Vector2.One", code)

    def test_focus_restore_is_eligible_deferred_and_owned_by_the_current_modal(self):
        focus = source("UI/RequiemFocus.cs")
        self.assertIn("node = node.GetParent()", focus)
        self.assertIn("if (node.IsQueuedForDeletion()) return false", focus)
        self.assertIn("if (stillActive != null && !stillActive()) return", focus)
        self.assertLess(focus.index("if (!Eligible(target)) continue"), focus.index("target.GrabFocus()"))
        for path in ("UI/NRequiemSheet.cs", "UI/NRunPassportScreen.cs", "UI/NHeroCreationScreen.cs"):
            code = source(path)
            self.assertIn("RequiemFocus.RestoreLater", code)
            self.assertIn("NModalContainer.Instance?.OpenModal, this", code)
            self.assertNotIn("CallDeferred(Control.MethodName.GrabFocus)", code)

    def test_shelf_keeps_zero_capacity_label_and_restores_focus_after_return(self):
        code = source("UI/NShoutShelfBar.cs")
        self.assertIn("restoreFocus", code)
        self.assertIn("RequiemFocus.RestoreLater", code)
        self.assertIn(".Prepend(_details)", code)
        self.assertIn("NModalContainer.Instance?.OpenModal == null", code)
        self.assertNotIn("Visible = _slots.Count > 0", code)
        self.assertNotIn("Visible = entries.Count > 0", code)

    def test_passport_is_responsive_native_modal_and_recovery_never_waits_on_dropped_popup(self):
        code = source("UI/NRunPassportScreen.cs")
        badge = source("UI/NRunStatusBadge.cs")
        self.assertIn("NRunPassportScreen.Show", badge)
        self.assertNotIn("OffsetLeft = -520", badge)
        self.assertNotIn("CustomMinimumSize = new Vector2(960", code)
        for token in ("OpenModal: null", "IScreenContext", "RequiemFocus.Scroll", "RequiemFocus.Handle",
                      "AnchorLeft = .05f", "_busy", "ReferenceEquals(_run, RequiemNet.State)"):
            self.assertIn(token, code)
        self.assertLess(code.index("Dismiss(); // release native OpenModal"), code.index("OperationRecovery.OfferRestore()"))
        recovery = source("Rules/OperationRecovery.cs")
        self.assertLess(recovery.index("OpenModal: null"), recovery.index("container.Add(popup)"))
        self.assertLess(recovery.index("ReferenceEquals(container.OpenModal, popup)"), recovery.index("popup.WaitForConfirmation"))
        confirm = source("Net/RequiemNet.cs").split("public static async Task<bool> Confirm", 1)[1]
        self.assertIn("while (Current() && container.OpenModal != null)", confirm)
        self.assertIn("ReferenceEquals(run, State)", confirm)
        self.assertLess(confirm.index("ReferenceEquals(container.OpenModal, popup)"), confirm.index("popup.WaitForConfirmation"))

    def test_runtime_metrics_are_opt_in_and_bounded_not_a_synthetic_native_result(self):
        code = source("Debugging/TechnicalMetrics.cs")
        for token in ("Capacity = 1024", "native-game-opt-in", "GetTotalAllocatedBytes", "ObjectNodeCount",
                      "ObjectOrphanNodeCount", "trait_redraw_requests", "RunDiagnostics.Describe", "if (Enabled)"):
            self.assertIn(token, code)
        benchmark = (ROOT / "tools/perf/Views/Program.cs").read_text()
        self.assertIn("not-engine-not-FPS-not-native-event-validation", benchmark)
        self.assertIn("before.Checksum != after.Checksum", benchmark)
        self.assertIn("actual-csharp-cache-microbenchmark", benchmark)
        self.assertIn("notice.Length > 2800", benchmark)


if __name__ == "__main__":
    unittest.main()
