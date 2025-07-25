using Generators;

namespace OneBarker.CopyCodeGenerators.Samples;

/*
 * Juliet is a struct.  Up until now all of our tests have been with classes.
 * Since structs are value types, a "copy constructor" for the type itself is
 * somewhat pointless.  We will include a similar struct to copy from.
 */

[EnableInitFrom(typeof(Juliet2))]
[EnableCopyFrom(typeof(Juliet))]
[EnableCopyFrom(typeof(Juliet2))]
[EnableUpdateFrom(typeof(Juliet))]
[EnableUpdateFrom(typeof(Juliet2))]
public partial struct Juliet
{
    public float   X;
    public float   Y;
    public string? Name;
}

public struct Juliet2
{
    public float   X;
    public float   Y;
    public string? Name;
}
