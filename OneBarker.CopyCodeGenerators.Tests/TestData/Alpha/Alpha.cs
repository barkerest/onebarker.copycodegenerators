using Generators;

namespace TestNamespace;

/*
 * A very basic class with a single property.
 */
[EnableInitFrom(typeof(Alpha))]
[EnableCopyFrom(typeof(Alpha))]
[EnableUpdateFrom(typeof(Alpha))]
public partial class Alpha
{
    public Alpha() { }

    public int Value { get; set; }
}
