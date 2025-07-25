using System.Linq;
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
            TargetObject      = target;
            SourceObjectNames = sourceNames;
            AttributeFound    = attributeFound;
            TargetObjectType = TargetObject is StructDeclarationSyntax
                                   ? "struct"
                                   : TargetObject is RecordDeclarationSyntax r
                                       ? !string.IsNullOrEmpty(r.ClassOrStructKeyword.Text)
                                         && r.ClassOrStructKeyword.Text != "class"
                                             ? "record " + r.ClassOrStructKeyword.Text
                                             : "record"
                                       : "class";
            TargetObjectParameters = ((TargetObject as RecordDeclarationSyntax)
                                      ?.ParameterList
                                      ?.Parameters)
                                     .GetValueOrDefault()
                                     .Select(x => x.Identifier.Text)
                                     .ToArray();
        }

        public readonly TypeDeclarationSyntax TargetObject;
        public readonly INamedTypeSymbol[]    SourceObjectNames;
        public readonly bool                  AttributeFound;
        public readonly string                TargetObjectType;
        public readonly string[]              TargetObjectParameters;
    }
}
