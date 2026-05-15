using Generators;

namespace OneBarker.CopyCodeGenerators.Samples;

[EnableUpdateExternal(typeof(System.Numerics.Vector2), typeof(System.Numerics.Vector3))]
public partial class Tango
{
    // NOT SUPPORTED!
    float Get_Z()
        => 123.4f;

    // CONSIDER SUPPORTING!
    float Get_Z(System.Numerics.Vector2 source)
        => source.X + source.Y;
}
