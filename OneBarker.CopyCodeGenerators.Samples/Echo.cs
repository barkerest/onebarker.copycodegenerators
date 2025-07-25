using Generators;

namespace OneBarker.CopyCodeGenerators.Samples;

/*
 * Echo is another record, this time using the primary constructor syntax.
 *
 * The copy constructor will function the same as before, but the non-copy constructor
 * must call the primary constructor with positional parameters.
 */

[EnableInitFrom(typeof(Echo))]
[EnableInitFrom(typeof(Echo2))]
public partial record Echo(string Name, int Age);

public record Echo2(string Name, int Age);