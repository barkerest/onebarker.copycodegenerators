using Generators;

namespace OneBarker.CopyCodeGenerators.Samples;

/*
 * India is testing that fields are included.
 * It also tests copying from a struct.
 */

[EnableInitFrom(typeof(India))]
[EnableUpdateFrom(typeof(India))]
[EnableCopyFrom(typeof(India))]
[EnableInitFrom(typeof(System.Numerics.Vector3))]
[EnableUpdateFrom(typeof(System.Numerics.Vector3))]
[EnableCopyFrom(typeof(System.Numerics.Vector3))]
public partial class India
{
    public float X;
    public float Y;
    public float Z { get; init; }
}
