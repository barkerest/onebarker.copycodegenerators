using Generators;

namespace Test;

/*
 * Sierra takes one of our common test structs and applies the UpdateExternal generator.
 * This tests to make sure the copy code generator is excluding indexer properties.
 */

[EnableUpdateExternal(typeof(System.Numerics.Vector2), typeof(System.Numerics.Vector2))]
public partial class Sierra
{
    
}
