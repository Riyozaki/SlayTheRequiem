using System.Text.Json;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using Xunit;

namespace SlayTheRequiem.Tests;

public sealed class SavedPayloadTests
{
    [Fact]
    public void Full_snapshots_ignore_object_and_named_property_order_only()
    {
        const string a = """{"id":"staff","floor_added_to_deck":7,"props":{"strings":[{"name":"Enchants","value":"fire:3:7"},{"name":"UnknownMod","value":"opaque"}],"ints":[{"name":"ChargesUsed","value":4},{"name":"Upgrades","value":2}]}}""";
        const string b = """{"props":{"ints":[{"value":2,"name":"Upgrades"},{"value":4,"name":"ChargesUsed"}],"strings":[{"value":"opaque","name":"UnknownMod"},{"value":"fire:3:7","name":"Enchants"}]},"floor_added_to_deck":7,"id":"staff"}""";
        Assert.True(SavedPayload.Equivalent(a, b));
        Assert.True(SavedPayload.Preserves(a, b));
        Assert.True(SavedPayload.Preserves(b, a));
    }

    [Theory]
    [InlineData("ints", "0")]
    [InlineData("bools", "false")]
    [InlineData("strings", "\"\"")]
    [InlineData("int_arrays", "[]")]
    [InlineData("card_arrays", "[]")]
    [InlineData("cards", "null")]
    [InlineData("model_ids", "null")]
    public void Legacy_snapshots_allow_added_typed_default_fields_but_comparison_is_directional(string kind, string value)
    {
        const string old = """{"id":"card"}""";
        var migrated = "{\"id\":\"card\",\"props\":{\"" + kind + "\":[{\"name\":\"NewSpireField\",\"value\":" + value + "}]}}";
        Assert.True(SavedPayload.Preserves(old, migrated));
        Assert.True(SavedPayload.Preserves("{\"id\":\"card\",\"props\":null}", migrated));
        Assert.False(SavedPayload.Preserves(migrated, old));
        Assert.False(SavedPayload.Equivalent(old, migrated));
        Assert.False(SavedPayload.Equivalent(migrated, old));
    }

    [Fact]
    public void New_default_names_may_join_an_existing_list_or_a_new_list()
    {
        const string old = """{"props":{"ints":[{"name":"Counter","value":9}]}}""";
        const string migrated = """{"props":{"ints":[{"name":"NewInt","value":0},{"name":"Counter","value":9}],"strings":[{"name":"NewString","value":""}]}}""";
        Assert.True(SavedPayload.Preserves(old, migrated));
        Assert.False(SavedPayload.Preserves(migrated, old));
        Assert.False(SavedPayload.Equivalent(old, migrated));
    }

    [Theory]
    [InlineData("ints", "0")]
    [InlineData("bools", "false")]
    [InlineData("strings", "\"\"")]
    [InlineData("int_arrays", "[]")]
    [InlineData("card_arrays", "[]")]
    public void A_dropped_unknown_field_is_never_excused_as_a_default(string kind, string value)
    {
        var source = "{\"props\":{\"" + kind + "\":[{\"name\":\"RemovedModField\",\"value\":" + value + "}]}}";
        Assert.False(SavedPayload.Preserves(source, "{\"props\":{}}"));
        Assert.False(SavedPayload.Equivalent(source, "{\"props\":{}}"));
    }

    [Theory]
    [InlineData("""{"id":"a"}""", """{"id":"b"}""")]
    [InlineData("""{"floor_added_to_deck":7}""", """{"floor_added_to_deck":8}""")]
    [InlineData("""{"enchantment":{"id":"fire","amount":3}}""", """{"enchantment":{"id":"fire","amount":1}}""")]
    [InlineData("""{"props":{"strings":[{"name":"Unknown","value":"opaque"}]}}""", """{"props":{"strings":[{"name":"Unknown","value":""}]}}""")]
    [InlineData("""{"props":{"ints":[{"name":"Counter","value":9}]}}""", """{"props":{"ints":[{"name":"Counter","value":8}]}}""")]
    [InlineData("""{"future":{"state":[1,2]}}""", "{}")]
    public void Changed_or_dropped_model_state_is_rejected(string source, string destination)
    {
        Assert.False(SavedPayload.Equivalent(source, destination));
        Assert.False(SavedPayload.Preserves(source, destination));
    }

    [Theory]
    [InlineData("""{"props":{"ints":[{"name":"New","value":1}]}}""")]
    [InlineData("""{"props":{"strings":[{"name":"New","value":"invented"}]}}""")]
    [InlineData("""{"props":{"ints":[{"name":"New","value":false}]}}""")]
    [InlineData("""{"props":{"bools":[{"name":"New","value":0}]}}""")]
    [InlineData("""{"props":{"int_arrays":[{"name":"New","value":[0]}]}}""")]
    [InlineData("""{"props":{"cards":[{"name":"New","value":{"id":"extra-card"}}]}}""")]
    [InlineData("""{"props":{"future":0}}""")]
    [InlineData("""{"current_upgrade_level":0}""")]
    [InlineData("""{"enchantment":null}""")]
    public void Only_named_default_additions_are_migrated_not_arbitrary_extra_json(string destination) =>
        Assert.False(SavedPayload.Preserves("{}", destination));

    [Theory]
    [InlineData("""{"id":"a","id":"a"}""")]
    [InlineData("""{"props":{"ints":[],"ints":[]}}""")]
    [InlineData("""{"props":{"ints":[{"name":"Same","value":0},{"name":"Same","value":0}]}}""")]
    [InlineData("""{"props":{"ints":[{"name":"Same","value":0}],"strings":[{"name":"Same","value":""}]}}""")]
    [InlineData("""{"props":{"ints":[{"name":"","value":0}]}}""")]
    public void Ambiguous_duplicate_or_empty_property_names_never_authorize_a_destructive_restore(string ambiguous)
    {
        Assert.False(SavedPayload.Equivalent(ambiguous, ambiguous));
        Assert.False(SavedPayload.Preserves(ambiguous, ambiguous));
        Assert.False(SavedPayload.Preserves("{}", ambiguous));
    }

    [Fact]
    public void Nested_card_properties_are_compared_recursively_but_card_and_int_array_order_is_state()
    {
        const string a = """{"props":{"cards":[{"name":"Imprint","value":{"id":"a","props":{"ints":[{"name":"Counter","value":2}]}}}],"card_arrays":[{"name":"Chest","value":[{"id":"a"},{"id":"b"}]}],"int_arrays":[{"name":"Materials","value":[1,2,0]}]}}""";
        var addedDefault = a.Replace("\"Counter\",\"value\":2}", "\"Counter\",\"value\":2},{\"name\":\"NewField\",\"value\":0}", StringComparison.Ordinal);
        Assert.True(SavedPayload.Preserves(a, addedDefault));
        Assert.False(SavedPayload.Equivalent(a, addedDefault));
        Assert.False(SavedPayload.Preserves(a, a.Replace("\"Counter\",\"value\":2", "\"Counter\",\"value\":1", StringComparison.Ordinal)));
        Assert.False(SavedPayload.Preserves(a, a.Replace("[{\"id\":\"a\"},{\"id\":\"b\"}]", "[{\"id\":\"b\"},{\"id\":\"a\"}]", StringComparison.Ordinal)));
        Assert.False(SavedPayload.Equivalent(a, a.Replace("[1,2,0]", "[2,1,0]", StringComparison.Ordinal)));
    }

    [Fact]
    public void Malformed_json_is_an_explicit_failure_not_an_empty_payload()
    {
        // JsonReaderException derives from JsonException; the contract is an explicit JSON failure, not
        // System.Text.Json's private implementation subtype (xUnit Throws<T> requires an exact type).
        Assert.ThrowsAny<JsonException>(() => SavedPayload.Preserves("not-json", "{}"));
        Assert.ThrowsAny<JsonException>(() => SavedPayload.Equivalent("{}", "not-json"));
    }
}
