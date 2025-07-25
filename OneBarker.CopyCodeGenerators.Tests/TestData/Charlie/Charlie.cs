using Generators;

/*
 * Charlie tests the ability to copy from types in different namespaces.
 * Specifically we want to guarantee that the generated code respects the namespaces provided.
 */

namespace Namespace1
{
    [EnableInitFrom(typeof(Namespace2.Charlie))]
    [EnableCopyFrom(typeof(Namespace2.Charlie))]
    [EnableUpdateFrom(typeof(Namespace2.Charlie))]
    public partial class Charlie
    {
        public int Alpha { get; set; }
    }
}

namespace Namespace2
{
    public partial class Charlie
    {
        public int Alpha { get; set; }
    }
}
