using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

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
            var sources = new List<INamedTypeSymbol>();

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
                        var paramSyntax = attributeSyntax.ArgumentList?.Arguments.FirstOrDefault();
                        if (paramSyntax is null) continue;
                        var typeofExpression = paramSyntax.Expression as TypeOfExpressionSyntax;
                        if (typeofExpression is null) continue;
                        var sourceTypeName =
                            context.SemanticModel.GetSymbolInfo(typeofExpression.Type).Symbol as INamedTypeSymbol;
                        if (sourceTypeName is null) continue;
                        sources.Add(sourceTypeName);
                    }
                }
            }

            if (sources.Count > 0)
            {
                return new CopyClassDeclarationSet(classDeclarationSyntax, sources.ToArray(), true);
            }

            return new CopyClassDeclarationSet(classDeclarationSyntax, null, false);
        }

        public delegate string CopyCommentGenerator(string targetClassName, string sourceClassName);

        public delegate string CopyMethodDeclarationGenerator(
            string                  targetClassName,
            string                  sourceClassName,
            string                  paramName,
            CopyClassDeclarationSet set
        );

        private static readonly Regex ValidNamePattern = new Regex("^[A-Z_][A-Z0-9_]*$", RegexOptions.IgnoreCase);

        private static void AddProperties(
            INamedTypeSymbol               symbol,
            HashSet<PropertyOrFieldSymbol> propertiesOrFields,
            bool                           includeNonPublic,
            bool                           includeReadOnly,
            bool                           includeInitOnly,
            HashSet<ISymbol>               handled = null
        )
        {
            if (handled is null) handled = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

            while (symbol != null)
            {
                if (!handled.Add(symbol)) return;

                var symProps = symbol.GetMembers()
                                     .OfType<IPropertySymbol>()
                                     .Where(x => x.IsWriteOnly != true);
                var symFields = symbol.GetMembers()
                                      .OfType<IFieldSymbol>()
                                      .Where(x => x.IsConst != true &&
                                                  ValidNamePattern.IsMatch(x.Name)
                                      );

                if (!includeNonPublic)
                {
                    symProps = symProps.Where(x => x.GetMethod                       != null &&
                                                   x.GetMethod.DeclaredAccessibility == Accessibility.Public
                    );
                    symFields = symFields.Where(x => x.DeclaredAccessibility == Accessibility.Public);
                }

                if (!includeReadOnly)
                {
                    symProps  = symProps.Where(x => x.IsReadOnly  != true);
                    symFields = symFields.Where(x => x.IsReadOnly != true);
                }

                if (!includeReadOnly && !includeInitOnly)
                {
                    symProps = symProps.Where(x => x.SetMethod != null && x.SetMethod.IsInitOnly != true);
                }
                
                symProps = symProps.Where(x =>
                    !x.GetAttributes()
                      .Any(y => SkipPropertySourceGenerator.IsAttribute(y.AttributeClass))
                );
                symFields = symFields.Where(x =>
                    !x.GetAttributes()
                      .Any(y => SkipPropertySourceGenerator.IsAttribute(y.AttributeClass))
                );

                foreach (var prop in symProps)
                {
                    propertiesOrFields.Add(new PropertyOrFieldSymbol(prop));
                }

                foreach (var field in symFields)
                {
                    propertiesOrFields.Add(new PropertyOrFieldSymbol(field));
                }

                foreach (var iface in symbol.Interfaces)
                {
                    AddProperties(
                        iface,
                        propertiesOrFields,
                        includeNonPublic,
                        includeReadOnly,
                        includeInitOnly,
                        handled
                    );
                }

                symbol = symbol.BaseType;
            }
        }

        private static IReadOnlyCollection<PropertyOrFieldSymbol> GetPropertiesAndFields(
            IEnumerable<INamedTypeSymbol> symbols,
            bool                          includeNonPublic,
            bool                          includeReadOnly, // return all properties.
            bool                          includeInitOnly // include init-only if not including read-only.
        )
        {
            var ret = new HashSet<PropertyOrFieldSymbol>();

            // handle partial definitions as separate symbols.
            foreach (var symbol in symbols)
            {
                AddProperties(symbol, ret, includeNonPublic, includeReadOnly, includeInitOnly);
            }

            return ret;
        }

        private static IReadOnlyCollection<PropertyOrFieldSymbol> GetPropertiesAndFields(
            INamedTypeSymbol symbol,
            bool             includeNonPublic,
            bool             includeReadOnly, // return all properties.
            bool             includeInitOnly // include init-only if not including read-only.
        )
        {
            var ret = new HashSet<PropertyOrFieldSymbol>();
            AddProperties(symbol, ret, includeNonPublic, includeReadOnly, includeInitOnly);
            return ret;
        }


        /// <summary>
        /// Generate code action.
        /// It will be executed on specific nodes (ClassDeclarationSyntax annotated with the copy attribute) changed by the user.
        /// </summary>
        /// <param name="context">Source generation context used to add source files.</param>
        /// <param name="compilation">Compilation used to provide access to the Semantic Model.</param>
        /// <param name="classDeclarationSets">Nodes annotated with the attributes that trigger the generate action.</param>
        /// <param name="methodDeclarationGenerator">Delegate to generate the method declaration.</param>
        /// <param name="commentGenerator">Delegate to generate the comment for the method.</param>
        /// <param name="extraMethodNameBase">The base name to use with the extra partial method declarations.</param>
        /// <param name="methodReturnsThis">Indicates if the method returns this.</param>
        /// <param name="methodReturnsCount">Indicates if the method returns the number of changed properties.</param>
        /// <param name="addBefore">Include the Before... partial method.</param>
        /// <param name="addAfter">Include the After... partial method.</param>
        internal static void GenerateCopyCode(
            this SourceProductionContext            context,
            Compilation                             compilation,
            ImmutableArray<CopyClassDeclarationSet> classDeclarationSets,
            CopyMethodDeclarationGenerator          methodDeclarationGenerator,
            CopyCommentGenerator                    commentGenerator,
            string                                  extraMethodNameBase,
            bool                                    methodReturnsThis,
            bool                                    methodReturnsCount,
            bool                                    addBefore,
            bool                                    addAfter
        )
        {
            var targetClasses = classDeclarationSets
                                .Select(set =>
                                    {
                                        var sem    = compilation.GetSemanticModel(set.TargetClass.SyntaxTree);
                                        var symbol = sem.GetDeclaredSymbol(set.TargetClass) as INamedTypeSymbol;
                                        return (set, symbol);
                                    }
                                )
                                .Where(x => x.symbol != null)
                                .ToArray();

            var noReturn = !methodReturnsCount && !methodReturnsThis;

            // Go through all filtered class declarations.
            foreach (var classDeclarationSet in targetClasses)
            {
                var classSymbol = classDeclarationSet.symbol;
                var methods     = new List<string>();
                var className   = classDeclarationSet.set.TargetClass.Identifier.Text;
                var classType   = classDeclarationSet.set.TargetClass is RecordDeclarationSyntax ? "record" : "class";

                // Records with parameterized constructors must have the primary constructor called.
                // And we need to generate some passthrough handlers to make that work.
                var recParamList =
                    ((classDeclarationSet.set.TargetClass as RecordDeclarationSyntax)?.ParameterList?.Parameters)
                    .GetValueOrDefault()
                    .Select(x => x.Identifier.Text)
                    .ToArray();

                var namespaceName = classSymbol.ContainingNamespace.ToDisplayString();
                var transforms    = new Dictionary<string, string>();

                var addInitPassthroughs = noReturn && recParamList.Length > 0;

                // include init-only for "no return" as this should be a constructor.
                var targetMembers = GetPropertiesAndFields(classSymbol, true, false, noReturn);

                foreach (var sourceClassSymbol in classDeclarationSet.set.SourceClassNames
                                                                     .OrderBy(x => x.ContainingNamespace.Name)
                                                                     .ThenBy(x => x.Name))
                {
                    string                                     sourceClassName;
                    IReadOnlyCollection<PropertyOrFieldSymbol> members;

                    if (SymbolEqualityComparer.Default.Equals(classSymbol, sourceClassSymbol))
                    {
                        // same class, copy 1-1
                        sourceClassName = className;
                        members         = targetMembers;
                    }
                    else
                    {
                        sourceClassName = $"{sourceClassSymbol.ContainingNamespace}.{sourceClassSymbol.Name}";
                        var sourceMembers = GetPropertiesAndFields(sourceClassSymbol, false, true, true);
                        members = targetMembers.Where(x => sourceMembers.Any(x.Equals)).ToArray();
                    }

                    var useInitPassthroughs = addInitPassthroughs && !string.Equals(sourceClassName, className);


                    var declaration = methodDeclarationGenerator(
                        className,
                        sourceClassName,
                        "source",
                        classDeclarationSet.set
                    );
                    var comment = commentGenerator(className, sourceClassName);


                    var body = new StringBuilder();

                    if (addBefore)
                    {
                        if (methodReturnsCount)
                        {
                            body.AppendFormat(
                                @"
    /// <summary>
    /// Method to run before the {0} method begins copying values.
    /// </summary>
    partial void Before{0}({1} source, ref int changeCount);
",
                                extraMethodNameBase,
                                sourceClassName
                            );
                        }
                        else
                        {
                            body.AppendFormat(
                                @"
    /// <summary>
    /// Method to run before the {0} method begins copying values.
    /// </summary>
    partial void Before{0}({1} source);
",
                                extraMethodNameBase,
                                sourceClassName
                            );
                        }
                    }

                    if (addAfter)
                    {
                        if (methodReturnsCount)
                        {
                            body.AppendFormat(
                                @"
    /// <summary>
    /// Method to run after the {0} method finishes copying values.
    /// </summary>
    partial void After{0}({1} source, ref int changeCount);
",
                                extraMethodNameBase,
                                sourceClassName
                            );
                        }
                        else
                        {
                            body.AppendFormat(
                                @"
    /// <summary>
    /// Method to run after the {0} method finishes copying values.
    /// </summary>
    partial void After{0}({1} source);
",
                                extraMethodNameBase,
                                sourceClassName
                            );
                        }
                    }

                    body.AppendFormat(
                        @"
    /// <summary>
    /// {0}
    /// </summary>
    {1}
    {{",
                        comment,
                        declaration
                    );

                    // make sure we aren't copying onto ourselves.
                    // if the method isn't returning, it would be a constructor, so no need for a check.
                    if (methodReturnsCount)
                    {
                        body.Append(
                            @"
        if (ReferenceEquals(null, source)) return 0;
        if (ReferenceEquals(this, source)) return 0;
        var changeCount = 0;"
                        );
                    }
                    else if (methodReturnsThis)
                    {
                        body.Append(
                            @"
        if (ReferenceEquals(null, source)) return this;
        if (ReferenceEquals(this, source)) return this;"
                        );
                    }
                    else
                    {
                        body.Append(
                            @"
        if (ReferenceEquals(null, source)) return;"
                        );
                    }

                    if (addBefore)
                    {
                        if (methodReturnsCount)
                        {
                            body.AppendFormat(
                                @"
        Before{0}(source, ref changeCount);",
                                extraMethodNameBase
                            );
                        }
                        else
                        {
                            body.AppendFormat(
                                @"
        Before{0}(source);",
                                extraMethodNameBase
                            );
                        }
                    }

                    // now handle the properties.
                    foreach (var p in members.OrderBy(x => x.Name))
                    {
                        if (!transforms.ContainsKey(p.Name))
                        {
                            if (addInitPassthroughs)
                            {
                                transforms[p.Name] = $@"
    /// <summary>
    /// Transforms the {p.Name} value before assigning the value to the target.
    /// </summary>
    static partial void {extraMethodNameBase}Transform_{p.Name}(ref {p.Type} value);
    
    /// <summary>
    /// Transforms the {p.Name} value and returns the new value.
    /// </summary>
    static {p.Type} PassthroughTransform_{p.Name}({p.Type} value)
    {{
        {extraMethodNameBase}Transform_{p.Name}(ref value);
        return value;
    }}";
                            }
                            else
                            {
                                transforms[p.Name] = $@"
    /// <summary>
    /// Transforms the {p.Name} value before assigning the value to the target.
    /// </summary>
    static partial void {extraMethodNameBase}Transform_{p.Name}(ref {p.Type} value);";
                            }
                        }

                        // property setting for parameterized properties is handled via the passthroughs only.
                        if (useInitPassthroughs && recParamList.Contains(p.Name)) continue;

                        var isNotNullable = !p.Type.IsValueType &&
                                            p.Type.NullableAnnotation != NullableAnnotation.Annotated;

                        var sourceName = "source_" + p.Name;
                        var targetName = "this_"   + p.Name;
                        if (methodReturnsCount)
                        {
                            if (p.Type.IsValueType)
                            {
                                // value types are easy.
                                body.AppendFormat(
                                    @"
        var {2} = this.{0};
        var {1} = source.{0};
        {3}Transform_{0}(ref {1});
        if (!{2}.Equals({1})) {{
            this.{0} = {1};
            changeCount++;
        }}",
                                    p.Name,
                                    sourceName,
                                    targetName,
                                    extraMethodNameBase
                                );
                            }
                            else if (isNotNullable)
                            {
                                // reference types can be nullable or non-nullable.
                                // non-nullable should not allow null to be set.
                                body.AppendFormat(
                                    @"
        var {2} = this.{0};
        var {1} = source.{0};
        if (!ReferenceEquals(null, {1})) {{
            {3}Transform_{0}(ref {1});
            if (!ReferenceEquals({2}, {1}) && !ReferenceEquals(null, {1}) && (ReferenceEquals(null, {2}) || !{2}.Equals({1}))) {{
                this.{0} = {1};
                changeCount++;
            }}
        }}",
                                    p.Name,
                                    sourceName,
                                    targetName,
                                    extraMethodNameBase
                                );
                            }
                            else
                            {
                                // nullable reference types don't need the null check on the source before processing.
                                body.AppendFormat(
                                    @"
        var {2} = this.{0};
        var {1} = source.{0};
        {3}Transform_{0}(ref {1});
        if (!ReferenceEquals({2}, {1}) && (ReferenceEquals(null, {2}) || (!ReferenceEquals(null, {2}) && !{2}.Equals({1})))) {{
            this.{0} = {1};
            changeCount++;
        }}",
                                    p.Name,
                                    sourceName,
                                    targetName,
                                    extraMethodNameBase
                                );
                            }
                        }
                        else
                        {
                            // don't check for changes, just copy the value over.
                            if (p.Type.IsValueType || !isNotNullable)
                            {
                                body.AppendFormat(
                                    @"
        var {1} = source.{0};
        {3}Transform_{0}(ref {1});
        this.{0} = {1};",
                                    p.Name,
                                    sourceName,
                                    targetName,
                                    extraMethodNameBase
                                );
                            }
                            else
                            {
                                body.AppendFormat(
                                    @"
        var {1} = source.{0};
        if (!ReferenceEquals(null, {1})) {{
            {3}Transform_{0}(ref {1});
            if (!ReferenceEquals(null, {1})) {{
                this.{0} = {1};
            }}
        }}",
                                    p.Name,
                                    sourceName,
                                    targetName,
                                    extraMethodNameBase
                                );
                            }
                        }
                    }

                    if (addAfter)
                    {
                        if (methodReturnsCount)
                        {
                            body.AppendFormat(
                                @"
        After{0}(source, ref changeCount);",
                                extraMethodNameBase
                            );
                        }
                        else
                        {
                            body.AppendFormat(
                                @"
        After{0}(source);",
                                extraMethodNameBase
                            );
                        }
                    }

                    if (methodReturnsCount)
                    {
                        body.Append("\n        return changeCount;");
                    }
                    else if (methodReturnsThis)
                    {
                        body.Append("\n        return this;");
                    }

                    body.Append("\n    }\n");

                    methods.Add(body.ToString());
                }

                var code = new StringBuilder();
                code.AppendFormat(
                    @"// <auto-generated/>

using System;

namespace {0};

#nullable enable
#pragma warning disable CS0109  // the member does not hide an inherited member.

partial {1} {2}
{{",
                    namespaceName,
                    classType,
                    className
                );
                
                foreach (var t in transforms.OrderBy(x => x.Key))
                {
                    code.Append(t.Value).Append('\n');
                }

                foreach (var m in methods)
                {
                    code.Append(m);
                }

                code.Append("}\n");

                // Add the source code to the compilation.
                context.AddSource($"{className}.g.cs", SourceText.From(code.ToString(), Encoding.UTF8));
            }
        }
    }
}
