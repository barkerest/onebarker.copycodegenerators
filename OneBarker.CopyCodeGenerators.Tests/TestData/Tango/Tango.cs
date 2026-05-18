using System.Numerics;
using Generators;

namespace OneBarker.CopyCodeGenerators.Samples;

/*
 * Tango gives us two more tests for those situations where your types aren't mapped 1:1.
 * 
 * Tango1 demonstrates that the Get_ method used by other copy methods is ignored for UpdateExternal.
 * Only X & Y are updated by Tango1.
 * 
 * Tango2 demonstrates the supported Get_ method for UpdateExternal.
 * X, Y, & Z are updated by Tango2.
 */

[EnableUpdateExternal(typeof(Vector2), typeof(Vector3))]
public partial class Tango1
{
    // NOT SUPPORTED, USE THE GET_*(source) variant. 
    float Get_Z()
        => 123.4f;
}

[EnableUpdateExternal(typeof(Vector2), typeof(Vector3))]
public partial class Tango2
{
    // SUPPORTED VARIANT.
    float Get_Z(Vector2 source)
        => source.X + source.Y;
}
