using Generators;

namespace OneBarker.CopyCodeGenerators.Samples;

/*
 * Delta is a record with read only properties, so basically a standard class.
 * Since records are read-only (usually), there is no reason to enable CopyFrom or UpdateFrom.
 * 
 * However, a "copy constructor" has specific rules in a record, so we will enable InitFrom
 * with both the same class and another class.  One is a copy constructor, and the other is not.
 *
 * Since we explicitly defined a parameterless constructor, we need to ensure the copy constructor
 * does not call the parameterless constructor and that the non-copy constructor does call it.
 *
 * There is one more potential issue with a copy constructor, since it doesn't get to call the
 * default constructor, it doesn't benefit from the default values for properties.  In this example,
 * the Name property must be set to a non-null value before exiting the constructor.  The non-copy
 * constructor uses the default constructor and then sets the values.
 * 
 * The copy constructor needs to address this somehow.  By contract, we should be fine.  We are
 * copying from another instance of the same type, so the values should be valid.  The transform
 * method also has a contract that should ensure the value stays non-null.  
 */

[EnableInitFrom(typeof(Delta))]
[EnableInitFrom(typeof(Delta2))]
public partial record Delta
{
    public Delta(){}
    
    public string Name { get; init; } = "";
    public int    Age  { get; init; }
}

public record Delta2
{
    public string Name { get; init; } = "";
    
    public int Age { get; init; }
}