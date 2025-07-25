using Generators;

namespace OneBarker.CopyCodeGenerators.Samples;

/*
 * Continuing with records, Foxtrot combines the primary constructor with an optional property.
 *
 * The copy constructor is straight-forward.  The non-copy must use the primary constructor
 * and then set the optional property (and only the optional property) in the body.
 */

[EnableInitFrom(typeof(Foxtrot))]
[EnableInitFrom(typeof(Foxtrot2))]
public partial record Foxtrot(string Name, int Age)
{
    public int? BirthYear { get; init; }
}

public record Foxtrot2(string Name, int Age, int? BirthYear);
