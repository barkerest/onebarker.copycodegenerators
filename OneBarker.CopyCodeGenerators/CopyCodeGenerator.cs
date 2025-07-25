using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace OneBarker.CopyCodeGenerators
{
    public class CopyCodeGenerator
    {
        /// <summary>
        /// The various supported return types for a defined method. 
        /// </summary>
        public enum MethodReturnType
        {
            /// <summary>
            /// The method returns nothing.
            /// </summary>
            None,

            /// <summary>
            /// The method returns nothing and is a constructor.
            /// </summary>
            Constructor,

            /// <summary>
            /// The method returns <b>this</b>.
            /// </summary>
            This,

            /// <summary>
            /// The method tracks and returns the number of changes made.
            /// </summary>
            Count,

            /// <summary>
            /// The method returns the source object.
            /// </summary>
            Source,

            /// <summary>
            /// The method returns the target object.
            /// </summary>
            Target,
        }

        /// <summary>
        /// A delegate to create the method declaration.
        /// </summary>
        public delegate string GetMethodDeclarationDelegate(
            string                  targetClassName,
            string                  sourceClassName,
            string                  paramName,
            CopyClassDeclarationSet set
        );

        /// <summary>
        /// A delegate to create the method comment.
        /// </summary>
        public delegate string GetMethodCommentDelegate(
            string                  targetClassName,
            string                  sourceClassName,
            CopyClassDeclarationSet set
        );

        #region Helpers

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

        #endregion

        private readonly GetMethodDeclarationDelegate _getMethodDeclaration;
        private readonly GetMethodCommentDelegate     _getMethodComment;
        private readonly string                       _extraMethodBaseName;
        private readonly MethodReturnType             _returnType;
        private readonly bool                         _addBeforeMethod;
        private readonly bool                         _addAfterMethod;
        private readonly bool                         _swapSourceAndTarget;

        /// <summary>
        /// Creates a copy code generator.
        /// </summary>
        /// <param name="getMethodDeclaration"></param>
        /// <param name="getMethodComment"></param>
        /// <param name="extraMethodBaseName"></param>
        /// <param name="returnType"></param>
        /// <param name="addBeforeMethod"></param>
        /// <param name="addAfterMethod"></param>
        /// <param name="swapSourceAndTarget"></param>
        public CopyCodeGenerator(
            GetMethodDeclarationDelegate getMethodDeclaration,
            GetMethodCommentDelegate     getMethodComment,
            string                       extraMethodBaseName,
            MethodReturnType             returnType,
            bool                         addBeforeMethod,
            bool                         addAfterMethod      = true,
            bool                         swapSourceAndTarget = false
        )
        {
            _getMethodDeclaration = getMethodDeclaration;
            _getMethodComment     = getMethodComment;
            _extraMethodBaseName  = extraMethodBaseName;
            _returnType           = returnType;
            _addBeforeMethod      = addBeforeMethod;
            _addAfterMethod       = addAfterMethod;
            _swapSourceAndTarget  = swapSourceAndTarget;
        }


        /// <summary>
        /// Generates the copy code.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="compilation"></param>
        /// <param name="classDeclarationSets"></param>
        public void Generate(
            SourceProductionContext                 context,
            Compilation                             compilation,
            ImmutableArray<CopyClassDeclarationSet> classDeclarationSets
        )
        {
            var targetClasses = classDeclarationSets
                                .Select(set =>
                                    {
                                        var sem    = compilation.GetSemanticModel(set.TargetObject.SyntaxTree);
                                        var symbol = sem.GetDeclaredSymbol(set.TargetObject) as INamedTypeSymbol;
                                        return (set, symbol);
                                    }
                                )
                                .Where(x => x.symbol != null)
                                .ToArray();

            // Go through all filtered class declarations.
            foreach (var classDeclarationSet in targetClasses)
            {
                var classSymbol = classDeclarationSet.symbol;
                var methods     = new List<string>();
                var className   = classDeclarationSet.set.TargetObject.Identifier.Text;
                var classType   = classDeclarationSet.set.TargetObjectType;

                // Records with parameterized constructors must have the primary constructor called.
                // And we need to generate some passthrough handlers to make that work.
                var recParamList = classDeclarationSet.set.TargetObjectParameters;

                var namespaceName       = classSymbol.ContainingNamespace.ToDisplayString();
                var addInitPassthroughs = _returnType == MethodReturnType.Constructor && recParamList.Length > 0;

                // these will be added to the source before the body of the method.
                var transforms = new Dictionary<string, string>();

                var targetMembers = GetPropertiesAndFields(
                    classSymbol,
                    true,
                    false,
                    _returnType == MethodReturnType.Constructor
                );

                foreach (var sourceClassSymbol in classDeclarationSet.set.SourceObjectNames
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


                    var declaration = _getMethodDeclaration(
                        className,
                        sourceClassName,
                        "source",
                        classDeclarationSet.set
                    );
                    var comment = _getMethodComment(className, sourceClassName, classDeclarationSet.set);


                    var body = new StringBuilder();

                    if (_addBeforeMethod)
                    {
                        if (_returnType == MethodReturnType.Count)
                        {
                            body.AppendFormat(
                                @"
    /// <summary>
    /// Method to run before the {0} method begins copying values.
    /// </summary>
    partial void Before{0}({1} source, ref int changeCount);
",
                                _extraMethodBaseName,
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
                                _extraMethodBaseName,
                                sourceClassName
                            );
                        }
                    }

                    if (_addAfterMethod)
                    {
                        if (_returnType == MethodReturnType.Count)
                        {
                            body.AppendFormat(
                                @"
    /// <summary>
    /// Method to run after the {0} method finishes copying values.
    /// </summary>
    partial void After{0}({1} source, ref int changeCount);
",
                                _extraMethodBaseName,
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
                                _extraMethodBaseName,
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
                    if (_returnType == MethodReturnType.Count)
                    {
                        body.Append(
                            @"
        if (ReferenceEquals(null, source)) throw new ArgumentNullException();
        if (ReferenceEquals(this, source)) return 0;
        var changeCount = 0;"
                        );
                    }
                    else if (_returnType == MethodReturnType.This)
                    {
                        body.Append(
                            @"
        if (ReferenceEquals(null, source)) throw new ArgumentNullException();
        if (ReferenceEquals(this, source)) return this;"
                        );
                    }
                    else
                    {
                        body.Append(
                            @"
        if (ReferenceEquals(null, source)) throw new ArgumentNullException();"
                        );
                    }

                    if (_addBeforeMethod)
                    {
                        if (_returnType == MethodReturnType.Count)
                        {
                            body.AppendFormat(
                                @"
        Before{0}(source, ref changeCount);",
                                _extraMethodBaseName
                            );
                        }
                        else
                        {
                            body.AppendFormat(
                                @"
        Before{0}(source);",
                                _extraMethodBaseName
                            );
                        }
                    }

                    // now handle the properties.
                    foreach (var p in members.OrderBy(x => x.Name))
                    {
                        if (!transforms.ContainsKey(p.Name))
                        {
                            transforms[p.Name] = $@"
    /// <summary>
    /// Transforms the {p.Name} value before assigning the value to the target.
    /// </summary>
    static partial void {_extraMethodBaseName}Transform_{p.Name}(ref {p.Type} value);";
                        }

                        if (useInitPassthroughs)
                        {
                            transforms[$"~{p.Name}_{sourceClassName}"] = $@"
    /// <summary>
    /// Transforms the {p.Name} value and returns the new value.
    /// </summary>
    static {p.Type} PassthroughTransform_{p.Name}({sourceClassName} source)
    {{
        if (ReferenceEquals(null, source)) throw new ArgumentNullException();
        var value = source.{p.Name};
        {_extraMethodBaseName}Transform_{p.Name}(ref value);
        return value;
    }}";
                        }


                        // property setting for parameterized properties is handled via the passthroughs only.
                        if (useInitPassthroughs && recParamList.Contains(p.Name)) continue;

                        var isNotNullable = !p.Type.IsValueType &&
                                            p.Type.NullableAnnotation != NullableAnnotation.Annotated;

                        var sourceName = "source_" + p.Name;
                        var targetName = "this_"   + p.Name;
                        if (_returnType == MethodReturnType.Count)
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
                                    _extraMethodBaseName
                                );
                            }
                            else if (isNotNullable)
                            {
                                // reference types can be nullable or non-nullable.
                                // non-nullable should be treated (mostly) like value types.
                                // we'll just throw in a null check before calling Equals()
                                body.AppendFormat(
                                    @"
        var {2} = this.{0};
        var {1} = source.{0};
        {3}Transform_{0}(ref {1});
        if (!ReferenceEquals({2}, {1}) && (ReferenceEquals(null, {2}) || !{2}.Equals({1}))) {{
            this.{0} = {1};
            changeCount++;
        }}",
                                    p.Name,
                                    sourceName,
                                    targetName,
                                    _extraMethodBaseName
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
                                    _extraMethodBaseName
                                );
                            }
                        }
                        else
                        {
                            // don't check for changes, just copy the value over.
                            body.AppendFormat(
                                @"
        var {1} = source.{0};
        {3}Transform_{0}(ref {1});
        this.{0} = {1};",
                                p.Name,
                                sourceName,
                                targetName,
                                _extraMethodBaseName
                            );
                        }
                    }

                    if (_addAfterMethod)
                    {
                        if (_returnType == MethodReturnType.Count)
                        {
                            body.AppendFormat(
                                @"
        After{0}(source, ref changeCount);",
                                _extraMethodBaseName
                            );
                        }
                        else
                        {
                            body.AppendFormat(
                                @"
        After{0}(source);",
                                _extraMethodBaseName
                            );
                        }
                    }

                    switch (_returnType)
                    {
                        case MethodReturnType.Target:
                        case MethodReturnType.This:
                            body.Append("\n        return this;");
                            break;
                        case MethodReturnType.Count:
                            body.Append("\n        return changeCount;");
                            break;
                        case MethodReturnType.Source:
                            body.Append("\n        return source;");
                            break;
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
