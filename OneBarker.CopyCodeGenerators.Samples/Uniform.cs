using Generators;

namespace OneBarker.CopyCodeGenerators.Samples;

/*
 * Uniform is designed to test the preference for member selection.
 *
 * Surprisingly, none of the other tests touched on this, though November came close.
 *
 * Given a choice between a method, property, or field, we want to ensure the appropriate
 * member is selected to maximize customization options.
 *
 * If a Get_ method is defined, it should have the highest preference.
 * A property should have the next highest preference, and a field should have the lowest preference.
 *
 * The below sample should end up with the Alpha & Bravo properties, _charlie field, and Get_Delta method
 * being used in the generated CopyTo code.
 *
 * The CopyFrom code will use the Delta property.
 */

[EnableCopyTo(typeof(Uniform1))]
[EnableCopyFrom(typeof(Uniform1))]
public partial class Uniform
{
    // Should not be used.
    private int _alpha;
    
    // Should be used.
    public  int Alpha
    {
        get => _alpha;
        set
        {
            _alpha   = value;
            _charlie = value * 2;
        }
    }
    
    // Should be used.
    public int Bravo { get; set; }

    // Should be used.
    private int _charlie;

    // Should not be used.
    private int _delta;
    
    // Should not be used.
    public  int Delta { get => _delta; set => _delta = value; }
    
    // Should be used.
    private int Get_Delta() => Delta;
}

public class Uniform1
{
    public int Alpha   { get; set; }
    public int Bravo   { get; set; }
    public int Charlie { get; set; }
    public int Delta   { get; set; }
}