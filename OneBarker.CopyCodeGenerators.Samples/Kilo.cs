using Generators;

namespace OneBarker.CopyCodeGenerators.Samples;

/*
 * Kilo makes sure we can enable init on record structs.
 * Since structs are value types, a "copy constructor" for the type itself is
 * somewhat pointless.  We will include a similar struct to copy from.
 */

[EnableInitFrom(typeof(Kilo2))]
public partial record struct Kilo(float X, float Y, string? Name);

public struct Kilo2
{
    public float   X;
    public float   Y;
    public string? Name;
}
