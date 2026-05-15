using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OneBarker.CopyCodeGenerators
{
    public static class Helpers
    {
        /// <summary>
        /// Checks whether the Node is annotated with the attribute and maps syntax context to the specific node type (CopyClassDeclarationSyntax).
        /// </summary>
        /// <param name="context">Syntax context, based on CreateSyntaxProvider predicate</param>
        /// <param name="fullAttributeName">The full name of the attribute that a class must be tagged with.</param>
        /// <returns>The specific cast and whether the attribute was found.</returns>
        internal static CopyClassDeclarationSet GetCopyClassDeclarationSetForSourceGen(
            this GeneratorSyntaxContext context,
            string                      fullAttributeName
        )
        {
            var classDeclarationSyntax = (TypeDeclarationSyntax)context.Node;

            // Go through all attributes of the class.
            var sources = new List<(INamedTypeSymbol,INamedTypeSymbol)>();

            foreach (AttributeListSyntax attributeListSyntax in classDeclarationSyntax.AttributeLists)
            {
                foreach (AttributeSyntax attributeSyntax in attributeListSyntax.Attributes)
                {
                    var attributeSymbol = context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol as IMethodSymbol;
                    if (attributeSymbol is null)
                        continue; // if we can't get the symbol, ignore it

                    var attributeName = attributeSymbol.ContainingType.ToDisplayString();

                    // Check the full name of the attribute.
                    if (attributeName == fullAttributeName)
                    {
                        if (attributeSyntax.ArgumentList is null) continue;
                        var argList     = attributeSyntax.ArgumentList.Arguments;
                        
                        if (argList.Count < 1) continue;
                        var paramSyntax = argList[0];
                        var typeofExpression = paramSyntax.Expression as TypeOfExpressionSyntax;
                        if (typeofExpression is null) continue;
                        var sourceTypeName =
                            context.SemanticModel.GetSymbolInfo(typeofExpression.Type).Symbol as INamedTypeSymbol;
                        if (sourceTypeName is null) continue;
                        if (argList.Count < 2)
                        {
                            // an attribute with a single argument uses the same type name in both arrays.
                            // the second array will be unused.
                            sources.Add((sourceTypeName, sourceTypeName));
                        }
                        else
                        {
                            // an attribute with two arguments has the source type as the first and the target type
                            // as the second argument.
                            paramSyntax      = argList[1];
                            typeofExpression = paramSyntax.Expression as TypeOfExpressionSyntax;
                            if (typeofExpression is null) continue;
                            var targetTypeName =
                                context.SemanticModel.GetSymbolInfo(typeofExpression.Type).Symbol as INamedTypeSymbol;
                            if (targetTypeName is null) continue;
                            sources.Add((sourceTypeName,targetTypeName));
                        }
                    }
                }
            }

            if (sources.Count > 0)
            {
                return new CopyClassDeclarationSet(classDeclarationSyntax, sources.ToArray(), true);
            }

            return new CopyClassDeclarationSet(classDeclarationSyntax, null, false);
        }

    }
}
