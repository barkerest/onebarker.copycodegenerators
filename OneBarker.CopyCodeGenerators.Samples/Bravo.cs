using Generators;

namespace TestNamespace;

/*
 * Bravo has three properties.
 * One is designated to be skipped by the generator.
 * The other two are reference types with differing nullability.
 *
 * We should see that the "IgnoredProperty" does not get included in any generated code
 * and that the "NonNullableString" has extra protections to prevent a null value assignment.
 */
[EnableInitFrom(typeof(Bravo))]
[EnableCopyFrom(typeof(Bravo))]
[EnableUpdateFrom(typeof(Bravo))]
public partial class Bravo
{
    public Bravo() { }

    public string  NonNullableString { get; set; } = "";
    public string? NullableString    { get; set; }

    [SkipOnCopy]
    public int IgnoredProperty { get; set; }
}
