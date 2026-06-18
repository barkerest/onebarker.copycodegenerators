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
 *
 * Finally, Bravo2 overrides two properties from Bravo.  The "IgnoredProperty" should still be
 * ignored even though the attribute was not added in again.  The "NullableString" property
 * should also be ignored for Bravo2.  And the default string value from Bravo should no
 * longer be used in Bravo2.
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

// A bug was found where having an interface on the inherited type causes the
// premature removal of skipped properties.
// In the bugged version, the [SkipOnCopy] is essentially ignored on the NullableString property below.
public interface IBravo
{
    
}

[EnableInitFrom(typeof(Bravo))]
[EnableCopyFrom(typeof(Bravo))]
[EnableUpdateFrom(typeof(Bravo))]

public partial class Bravo2 : Bravo, IBravo
{
    // ensure this is now ignored.
    [SkipOnCopy]
    public override string? NullableString { get; set; }

    // ensure this is still ignored.
    public override int IgnoredProperty { get; set; }
}