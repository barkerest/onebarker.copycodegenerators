using Generators;

namespace OneBarker.CopyCodeGenerators.Samples;

/*
 * The Papa class continues on with testing external copying, this time with a struct.
 * The source parameter should be unmodified, but the target parameter must have the 'ref' modifier.
 * The W field should remain untouched since it is readonly.
 */

[EnableUpdateExternal(typeof(Papa2), typeof(Papa2))]
public partial class Papa
{
    
}

public struct Papa2
{
    public          int X;
    public          int Y;
    public          int Z;
    public readonly int W;
}
