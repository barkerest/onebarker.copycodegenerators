using Generators;

namespace OneBarker.CopyCodeGenerators.Samples;

/*
 * Oscar is the first test of the external copying.
 * The Oscar class itself is nothing more than a bridge, it has no access to private
 * parts within the target class (W should not be touched).
 */

[EnableUpdateExternal(typeof(Oscar1), typeof(Oscar1))]
public partial class Oscar
{
    
}

public class Oscar1
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int W { get; private set; }
}