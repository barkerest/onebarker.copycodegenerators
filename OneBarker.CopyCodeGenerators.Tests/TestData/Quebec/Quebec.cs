using Generators;

namespace OneBarker.CopyCodeGenerators.Samples;

/*
 * Quebec extends the external test to using two different types.
 * Quebec copies from a struct to a class.
 * The W, X, & Y properties should be set in the target.
 */

[EnableUpdateExternal(typeof(QuebecSource),typeof(QuebecTarget))]
public partial class Quebec
{
    
}

public struct QuebecSource
{
    public          int X;
    public          int Y;
    public          int Z;
    public readonly int W;
}

public class QuebecTarget
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; private set; }
    public int W { get; set; }
}
