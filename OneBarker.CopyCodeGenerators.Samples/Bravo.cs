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
    
    public virtual string? NullableString    { get; set; }

    [SkipOnCopy]
    public virtual int IgnoredProperty { get; set; }

    private static string DefaultString() => "~something~";
}

[EnableInitFrom(typeof(Bravo))]
[EnableCopyFrom(typeof(Bravo))]
[EnableUpdateFrom(typeof(Bravo))]

public partial class Bravo2 : Bravo
{
    // ensure this is now ignored.
    [SkipOnCopy]
    public override string? NullableString { get; set; }

    // ensure this is still ignored.
    public override int IgnoredProperty { get; set; }
}