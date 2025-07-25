using Generators;

namespace OneBarker.CopyCodeGenerators.Samples;

/*
 * Mike is testing the SkipOnCopy attribute.
 * In all scenarios, the property should be skipped.
 */

[EnableInitFrom(typeof(Mike))]
[EnableInitFrom(typeof(Mike2))]
[EnableCopyFrom(typeof(Mike))]
[EnableCopyFrom(typeof(Mike2))]
[EnableCopyTo(typeof(Mike2))]
[EnableUpdateFrom(typeof(Mike))]
[EnableUpdateFrom(typeof(Mike2))]
[EnableUpdateTarget(typeof(Mike2))]
public partial class Mike
{
    public int ValueToCopy { get; set; }
    
    [SkipOnCopy]
    public int ValueToIgnore { get; set; }
}

public class Mike2
{
    public int ValueToCopy { get; set; }
    
    public int ValueToIgnore { get; set; }
}
