using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OneBarker.CopyCodeGenerators
{
    public class CopyClassDeclarationSet
    {
        public CopyClassDeclarationSet(
            TypeDeclarationSyntax target,
            INamedTypeSymbol[]    sourceNames,
            bool                  attributeFound
        )
        {
            TargetClass      = target;
            SourceClassNames = sourceNames;
            AttributeFound   = attributeFound;
        }

        public readonly TypeDeclarationSyntax TargetClass;
        public readonly INamedTypeSymbol[]    SourceClassNames;
        public readonly bool                  AttributeFound;
    }
}
