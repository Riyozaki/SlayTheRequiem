"""Regression for the source-only duplicate type gate; not semantic game compilation."""
from pathlib import Path
import tempfile
import unittest

import check_csharp_declarations as gate


class DeclarationGateTests(unittest.TestCase):
    def errors(self, *texts):
        return gate.conflicts({f"part-{i}.cs": text.encode() for i, text in enumerate(texts)})[0]

    def test_the_actual_duplicate_patch_shape_is_not_valid_syntax_only_acceptance(self):
        code = "namespace Equipment; public static class PaperDollDeathPatch { public static void Prefix() {} }"
        self.assertEqual(1, len(self.errors(code + code.removeprefix("namespace Equipment; "))))

    def test_duplicate_non_partial_across_files_is_rejected(self):
        self.assertEqual(1, len(self.errors("namespace N; class A {}", "namespace N { class A {} }")))

    def test_all_partial_declarations_of_one_kind_are_legal(self):
        self.assertFalse(self.errors("namespace N; partial class A {}", "namespace N { partial class A {} }"))

    def test_one_non_partial_or_different_kind_is_not_legal(self):
        self.assertTrue(self.errors("partial class A {}", "class A {}"))
        self.assertTrue(self.errors("partial class A {}", "partial struct A {}"))
        self.assertTrue(self.errors("partial record class A;", "partial record struct A;"))

    def test_nesting_and_generic_arity_are_distinct_but_same_nested_type_conflicts(self):
        self.assertFalse(self.errors("class A { class Item {} } class B { class Item {} } class A<T> {}"))
        self.assertTrue(self.errors("partial class A<T> { class Item {} }", "partial class A<U> { class Item {} }"))

    def test_namespace_and_file_local_types_are_not_global_collisions(self):
        self.assertFalse(self.errors("namespace A { class Item {} }", "namespace B; class Item {}"))
        self.assertFalse(self.errors("file class Local { class Nested {} }", "file class Local { class Nested {} }"))
        self.assertTrue(self.errors("file class Local {} file class Local {}"))

    def test_enum_and_delegate_share_type_namespace(self):
        self.assertTrue(self.errors("enum A { One } delegate void A();"))
        self.assertFalse(self.errors("delegate void A(); delegate void A<T>(T item);"))

    def test_comments_strings_and_member_identifiers_do_not_create_types(self):
        self.assertFalse(self.errors('class A { string S = "class A {}"; } // class A {}'))
        self.assertFalse(self.errors('class A { void Item() {} } class Item {}'))

    def test_project_includes_external_compile_links_but_excludes_tests_and_generated_output(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); project = root / "project"; project.mkdir()
            (project / "Tests").mkdir(); (project / "obj").mkdir()
            for path in (project / "A.cs", project / "Tests/B.cs", project / "obj/Generated.cs", root / "Linked.cs"):
                path.write_text("class A {}")
            csproj = project / "Demo.csproj"
            csproj.write_text('<Project><ItemGroup><Compile Remove="Tests/**" /><Compile Include="../Linked.cs" /></ItemGroup></Project>')
            self.assertEqual({project / "A.cs", root / "Linked.cs"}, set(gate.project_sources(csproj)))

    def test_missing_explicit_compile_link_and_invalid_source_fail_closed(self):
        with self.assertRaisesRegex(ValueError, "parse error"):
            self.errors("class A { broken ??? }")
        with tempfile.TemporaryDirectory() as directory:
            project = Path(directory) / "Demo.csproj"
            project.write_text('<Project><ItemGroup><Compile Include="Missing.cs" /></ItemGroup></Project>')
            with self.assertRaisesRegex(ValueError, "Missing Compile"):
                gate.project_sources(project)


if __name__ == "__main__":
    unittest.main()
