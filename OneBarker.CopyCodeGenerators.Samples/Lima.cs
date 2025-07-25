using Generators;

namespace OneBarker.CopyCodeGenerators.Samples;

/*
 * All the previous tests were copying data into the class being extended.
 * Lima reverses this and is copying data to another object.
 *
 * This reverses the role of 'this' in the generated code.  And it changes
 * the set of properties/fields that should be used.  Our code does not have
 * access to non-public members in the target anymore, so we need to play nice.
 *
 * The only property being copied in this example should be 'Value'.
 *
 * Lima3 is a struct and tests out the 'ref' keyword is included.
 */

[EnableCopyTo(typeof(Lima2))]
[EnableUpdateTarget(typeof(Lima2))]
[EnableCopyTo(typeof(Lima3))]
[EnableUpdateTarget(typeof(Lima3))]
public partial class Lima
{
    public int Value { get; init; }
    
    public int ReadOnlyValue { get; set; }
    
    public string? Name { get; set; }
}


public class Lima2
{
    public int Value { get; set; }
    
    public int ReadOnlyValue { get; init; }
    
    public string? Name { get; private set; }
}

public struct Lima3
{
    public int Value;
}