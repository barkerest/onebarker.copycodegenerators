using System.Runtime.Versioning;
using Generators;

namespace OneBarker.CopyCodeGenerators.Samples;

/*
 * Golf is designed to test inheritance.  All three properties should be copied between Golf instances,
 * but only the appropriate properties should be included in the samples with the ancestors.
 */

[EnableInitFrom(typeof(Golf))]
[EnableCopyFrom(typeof(Golf))]
[EnableUpdateFrom(typeof(Golf))]
[EnableInitFrom(typeof(PreGolf))]
[EnableCopyFrom(typeof(PreGolf))]
[EnableUpdateFrom(typeof(PreGolf))]
[EnableInitFrom(typeof(PrePreGolf))]
[EnableCopyFrom(typeof(PrePreGolf))]
[EnableUpdateFrom(typeof(PrePreGolf))]
public partial class Golf : PreGolf
{
    public float Z { get; set; }
}

public class PreGolf : PrePreGolf
{
    public float Y { get; set; }
}

public class PrePreGolf
{
    public float X { get; set; }
}