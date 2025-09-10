using Generators;

namespace OneBarker.CopyCodeGenerators.Samples;

/*
 * November demoes a few ideas.
 *
 * We utilize Get_ methods to provide a value for a property in the target type.
 *
 * We also utilize prefixed private field names (eg - _value1) that will only
 * be available from code generated in that containing class.
 *
 * We also define a constant as the default string value inside November2.
 * This affects the "UpdateFrom" code, but not the "UpdateTarget" code generated
 * in the November class.
 */

[EnableUpdateTarget(typeof(November2))]
public partial class November
{
    private int _value1;

    public int TheFirstValue
    {
        get => _value1;
        set => _value1 = value;
    }
    
    public int TheSecondValue { get; set; }
    
    public int Get_Value2() => TheSecondValue;
    
    public string? NullableString { get; set; }

    public string Get_NonNullableString() => NullableString ?? "";
}


[EnableUpdateFrom(typeof(November))]
public partial class November2
{
    public int Value1 { get; set; }
    
    public int Value2 { get; set; }

    private string? _nullableString;
    
    public string NonNullableString { get; set; } = "";

    private const string MyDefaultString = "~~~~";
}
