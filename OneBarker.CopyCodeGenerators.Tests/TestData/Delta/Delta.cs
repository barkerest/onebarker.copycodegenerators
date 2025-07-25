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
