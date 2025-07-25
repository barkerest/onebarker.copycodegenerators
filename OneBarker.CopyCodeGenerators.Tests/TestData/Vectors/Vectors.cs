using System;
using Generators;

namespace TestNamespace;

/*
 * Vectors tests a more complex data structure.
 * Vector2 includes the ability to initialize from the Numerics library struct Vector2.
 * Coordinate is defined as a record.
 * 
 * NOTE: Any type using EnableInitFrom should define a default constructor.
 *       It is not always required, but usually is.
 *       When a default constructor is defined, the generated constructors will call it.
 *
 * NOTE: Source classes are sorted by name, not by attribute position (see Vector1 below).
 * 
 * NOTE: Properties are sorted by name, not by defined order (see Vector2-Vector4 below).
 */


[EnableCopyFrom(typeof(Vector1))]
[EnableCopyFrom(typeof(Vector3))]
[EnableCopyFrom(typeof(Vector2))]
[EnableCopyFrom(typeof(Vector4))]
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
    public float Y { get; set; }
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
