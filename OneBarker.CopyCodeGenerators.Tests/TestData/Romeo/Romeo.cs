using Generators;

namespace OneBarker.CopyCodeGenerators.Samples;

/*
 * Quebec copied from a struct to a class, Romoe copies from a class to a struct.
 * The X, Y, & Z fields should be set in the target.
 */

[EnableUpdateExternal(typeof(RomeoSource), typeof(RomeoTarget))]
public partial class Romeo
{
    
}

public class RomeoSource
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; private set; }
    public int W { get; set; }
}

public struct RomeoTarget
{
    public          int X;
    public          int Y;
    public          int Z;
    public readonly int W;
}

