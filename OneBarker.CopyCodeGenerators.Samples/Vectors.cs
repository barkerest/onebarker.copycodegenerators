using Generators;

namespace OneBarker.CopyCodeGenerators.Samples;

[EnableCopyFrom(typeof(Vector1))]
[EnableCopyFrom(typeof(Vector3))]
[EnableCopyFrom(typeof(Vector4))]
[EnableCopyFrom(typeof(Vector2))]
public partial class Vector1
{
    public string Name     { get; init; } = "";
    public float  X        { get; set; }
    public bool   IsVector => true;
}

[EnableInitFrom(typeof(System.Numerics.Vector2))]
[EnableUpdateFrom(typeof(System.Numerics.Vector2))]
[EnableUpdateFrom(typeof(Vector1))]
[EnableUpdateFrom(typeof(Vector2))]
[EnableUpdateFrom(typeof(Vector3))]
public partial class Vector2 : Vector1
{
    public Vector2() { }
    
    public float Y                   { get; set; }
}

[EnableInitFrom(typeof(Vector2))]
[EnableInitFrom(typeof(Vector3))]
[EnableInitFrom(typeof(Vector4))]
[EnableInitFrom(typeof(Coordinate))]
public partial class Vector3 : Vector2
{
    public Vector3() { }

    public float Z { get; set; }
}

[EnableCopyFrom(typeof(Vector3))]
[EnableCopyFrom(typeof(Vector4))]
public partial class Vector4 : Vector3
{
    public float W { get; set; }
}

[EnableInitFrom(typeof(Coordinate))]
[EnableInitFrom(typeof(Vector3))]
public partial record Coordinate
{
    // Coordinate does not have a default constructor.
    // This constructor and the two generated constructors are all stand-alone.
    public Coordinate(float x, float y, float z)
    {
        X        = x;
        Y        = y;
        Z        = z;
        IsVector = false;
    }
    
    public float X        { get; init; }
    public float Y        { get; init; }
    public float Z        { get; init; }
    public bool  IsVector { get; init; }
}

// records with a parameter list require a different approach.
// the parameterized this() must be called by the generated constructor except in the case of the "copy constructor"
// which must not call the parameterized this().
[EnableInitFrom(typeof(Coordinate2))]
[EnableInitFrom(typeof(Vector2))]
public partial record Coordinate2(float X, float Y);
