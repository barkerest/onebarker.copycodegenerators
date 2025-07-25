using Generators;

namespace OneBarker.CopyCodeGenerators.Samples;

/*
 * Hotel is testing the interaction with interfaces and init-only properties.
 *
 * The generated constructor should update all the fields.
 * The copy and update methods should update two of the fields.
 */

[EnableInitFrom(typeof(Hotel))]
[EnableCopyFrom(typeof(Hotel))]
[EnableUpdateFrom(typeof(Hotel))]
[EnableInitFrom(typeof(IHotel))]
[EnableCopyFrom(typeof(IHotel))]
[EnableUpdateFrom(typeof(IHotel))]
public partial class Hotel : IHotel
{
    public Hotel(){}
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; init; }
}

public interface IHotel
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; }
}
