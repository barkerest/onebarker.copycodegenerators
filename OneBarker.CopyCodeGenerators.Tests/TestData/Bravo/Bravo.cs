using Generators;

namespace TestNamespace;

/*
 * Bravo has three properties.
 * One is designated to be skipped by the generator.
 * The other two are reference types with differing nullability.
 *
 * We should see that the "IgnoredProperty" does not get included in any generated code
 * and that the "NonNullableString" has extra protections to prevent a null value assignment.
 *
 * We are also taking this opportunity to ensure that the copy code correctly identifies the
 * value we want to use as a default for non-nullable strings.  This is a newer behavior
 * meant to make the null-checking more robust.  The code will try to find a default/empty value
 * in the value type (eg - String.Empty), but if we define a static method that returns the
 * target type, then that should be used instead.
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

    private static string DefaultString() => "~something~";
}
