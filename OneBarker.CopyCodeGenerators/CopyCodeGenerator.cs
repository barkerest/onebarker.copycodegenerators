using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
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
            string                  paramName1,
            [CanBeNull] string      paramName2,
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

        private static void AddValues(
            INamedTypeSymbol     symbol,
            HashSet<ValueSymbol> valueSymbols,
            bool                 includeNonPublic,
            bool                 includeReadOnly,
            bool                 includeInitOnly,
            HashSet<ISymbol>     handled                  = null,
            bool                 includeFields            = true,
            bool                 includeProps             = true,
            bool                 includeMethods           = true,
            INamedTypeSymbol     includeSingleParamMethod = null,
            bool removeSkipped = true
        )
        {
            if (handled is null) handled = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

            while (symbol != null)
            {
                if (!handled.Add(symbol)) break;

                
                var symProps = includeProps
                                   ? symbol.GetMembers()
                                           .OfType<IPropertySymbol>()
                                           .Where(x => !x.IsWriteOnly &&
                                                       x.Parameters.IsEmpty
                                           )
                                   : Array.Empty<IPropertySymbol>();
                var symFields = includeFields
                                    ? symbol.GetMembers()
                                            .OfType<IFieldSymbol>()
                                            .Where(x => x.IsConst != true &&
                                                        ValidNamePattern.IsMatch(x.Name)
                                            )
                                    : Array.Empty<IFieldSymbol>();
                var symGets = includeMethods
                                  ? symbol.GetMembers()
                                          .OfType<IMethodSymbol>()
                                          .Where(x => !x.IsStatic                            &&
                                                      !x.ReturnsVoid                         &&
                                                      !x.IsGenericMethod                     &&
                                                      !x.IsPartialDefinition                 &&
                                                      !x.IsAbstract                          &&
                                                      x.Arity      == 0                      &&
                                                      x.MethodKind != MethodKind.PropertyGet &&
                                                      (
                                                          (
                                                              includeSingleParamMethod == null &&
                                                              x.Parameters.IsEmpty
                                                          ) ||
                                                          (
                                                              includeSingleParamMethod != null &&
                                                              x.Parameters.Length      == 1    &&
                                                              SymbolEqualityComparer.Default.Equals(
                                                                  x.Parameters[0].Type,
                                                                  includeSingleParamMethod
                                                              )
                                                          )
                                                      ) &&
                                                      x.Name.StartsWith("Get_", StringComparison.OrdinalIgnoreCase)
                                          )
                                  : Array.Empty<IMethodSymbol>();
                
                if (!includeNonPublic)
                {
                    symProps = symProps.Where(x => x.GetMethod                       != null &&
                                                   x.GetMethod.DeclaredAccessibility == Accessibility.Public
                    );
                    symFields = symFields.Where(x => x.DeclaredAccessibility == Accessibility.Public);
                    symGets   = symGets.Where(x => x.DeclaredAccessibility   == Accessibility.Public);
                }

                if (!includeReadOnly)
                {
                    symProps  = symProps.Where(x => x.IsReadOnly  != true);
                    symFields = symFields.Where(x => x.IsReadOnly != true);
                    if (!includeNonPublic)
                    {
                        symProps = symProps.Where(x =>
                            x.SetMethod != null && x.SetMethod.DeclaredAccessibility == Accessibility.Public
                        );
                    }

                    symGets = Array.Empty<IMethodSymbol>();
                }

                if (!includeReadOnly && !includeInitOnly)
                {
                    symProps = symProps.Where(x => x.SetMethod != null && x.SetMethod.IsInitOnly != true);
                }
                
                foreach (var prop in symProps)
                {
                    valueSymbols.Add(new ValueSymbol(prop));
                }

                foreach (var field in symFields)
                {
                    valueSymbols.Add(new ValueSymbol(field));
                }

                foreach (var get in symGets)
                {
                    valueSymbols.Add(new ValueSymbol(get));
                }

                foreach (var iface in symbol.Interfaces)
                {
                    AddValues(
                        iface,
                        valueSymbols,
                        includeNonPublic,
                        includeReadOnly,
                        includeInitOnly,
                        handled,
                        includeFields,
                        includeProps,
                        includeMethods,
                        includeSingleParamMethod,
                        removeSkipped: false
                    );
                }

                symbol = symbol.BaseType;
            }

            if (removeSkipped)
            {
                valueSymbols.RemoveWhere(x
                    => x.Attributes.Any(y => SkipPropertySourceGenerator.IsAttribute(y.AttributeClass))
                );
            }
        }

        private static IReadOnlyCollection<ValueSymbol> GetValues(
            IEnumerable<INamedTypeSymbol> symbols,
            bool                          includeNonPublic,
            bool                          includeReadOnly, // return all properties.
            bool                          includeInitOnly, // include init-only if not including read-only.
            INamedTypeSymbol              includeSingleParamMethod = null,
            bool                          includeFields            = true,
            bool                          includeProps             = true,
            bool                          includeMethods           = true
        )
        {
            var ret = new HashSet<ValueSymbol>();

            // handle partial definitions as separate symbols.
            foreach (var symbol in symbols)
            {
                AddValues(
                    symbol,
                    ret,
                    includeNonPublic,
                    includeReadOnly,
                    includeInitOnly,
                    null,
                    includeFields,
                    includeProps,
                    includeMethods,
                    includeSingleParamMethod
                );
            }

            return ret;
        }

        private static IReadOnlyCollection<ValueSymbol> GetValues(
            INamedTypeSymbol symbol,
            bool             includeNonPublic,
            bool             includeReadOnly, // return all properties and get_ methods.
            bool             includeInitOnly, // include init-only if not including read-only.
            INamedTypeSymbol includeSingleParamMethod = null,
            bool             includeFields            = true,
            bool             includeProps             = true,
            bool             includeMethods           = true
        )
        {
            var ret = new HashSet<ValueSymbol>();
            AddValues(
                symbol,
                ret,
                includeNonPublic,
                includeReadOnly,
                includeInitOnly,
                null,
                includeFields,
                includeProps,
                includeMethods,
                includeSingleParamMethod
            );
            return ret;
        }

        private static string GetNullDefaultFromCandidates(
            ITypeSymbol type,
            ITypeSymbol container,
            ISymbol[]   candidateMembers,
            string      targetName
        )
        {
            if (candidateMembers.Length < 1) return null;
            var isContainerType = container.Equals(type, SymbolEqualityComparer.Default);
            var methodNames = isContainerType
                                  ? new string[]
                                    {
                                        "Default",
                                        "Empty",
                                    }
                                  : new string[]
                                    {
                                        $"Default{type?.Name}For{targetName}",
                                        $"Empty{type?.Name}For{targetName}",
                                        $"Default{type?.Name}",
                                        $"Empty{type?.Name}",
                                    };

            var propertyNames = isContainerType
                                    ? new string[]
                                      {
                                          "Default",
                                          "Empty",
                                          "Instance",
                                          "Value",
                                      }
                                    : new string[]
                                      {
                                          $"Default{type?.Name}For{targetName}",
                                          $"Empty{type?.Name}For{targetName}",
                                          $"Default{type?.Name}",
                                          $"Empty{type?.Name}",
                                      };

            var fieldNames = isContainerType
                                 ? new string[]
                                   {
                                       "Default",
                                       "Empty",
                                       "Instance",
                                       "Value",
                                       "_default",
                                       "_empty",
                                       "_instance",
                                       "_value",
                                   }
                                 : new string[]
                                   {
                                       $"Default{type?.Name}For{targetName}",
                                       $"Empty{type?.Name}For{targetName}",
                                       $"Default{type?.Name}",
                                       $"Empty{type?.Name}",
                                       $"_default{type?.Name}For{targetName}",
                                       $"_empty{type?.Name}For{targetName}",
                                       $"_default{type?.Name}",
                                       $"_empty{type?.Name}",
                                   };

            var candidateMethods = candidateMembers
                                   .OfType<IMethodSymbol>()
                                   .Where(x => !x.ReturnsVoid
                                               && x.ReturnType.Equals(
                                                   type,
                                                   SymbolEqualityComparer.Default
                                               )
                                               && x.Parameters.IsEmpty
                                   )
                                   .ToArray();

            if (candidateMethods.Length > 0)
            {
                if (candidateMethods.Length == 1) return $"{container.Name}.{candidateMethods[0].Name}()";
                foreach (var name in methodNames)
                {
                    var candidate = candidateMethods.FirstOrDefault(x => x.Name.Equals(name, StringComparison.Ordinal));
                    if (!ReferenceEquals(candidate, null)) return $"{container.Name}.{candidate.Name}()";
                }
            }

            var candidateProperties = candidateMembers
                                      .OfType<IPropertySymbol>()
                                      .Where(x => x.Type.Equals(type, SymbolEqualityComparer.Default)
                                      )
                                      .ToArray();

            if (candidateProperties.Length > 0)
            {
                if (candidateProperties.Length == 1)
                    return $"{container.Name}.{candidateProperties[0].Name}";
                foreach (var name in propertyNames)
                {
                    var candidate =
                        candidateProperties.FirstOrDefault(x => x.Name.Equals(name, StringComparison.Ordinal));
                    if (!ReferenceEquals(null, candidate)) return $"{container.Name}.{candidate.Name}";
                }
            }

            var candidateFields = candidateMembers
                                  .OfType<IFieldSymbol>()
                                  .Where(x => x.Type.Equals(type, SymbolEqualityComparer.Default)
                                  )
                                  .ToArray();

            if (candidateFields.Length > 0)
            {
                if (candidateFields.Length == 1) return $"{container.Name}.{candidateFields[0].Name}";

                foreach (var name in fieldNames)
                {
                    var candidate =
                        candidateFields.FirstOrDefault(x => x.Name.Equals(name, StringComparison.Ordinal));
                    if (!ReferenceEquals(null, candidate)) return $"{container.Name}.{candidate.Name}";
                }
            }

            return null;
        }

        private static string GetNullDefault(
            ITypeSymbol      type,
            INamedTypeSymbol containingClass,
            string           targetName
        )
        {
            return GetNullDefaultFromCandidates(
                       type,
                       containingClass,
                       containingClass.GetMembers().Where(x => x.IsStatic).ToArray(),
                       targetName
                   )
                   ?? GetNullDefaultFromCandidates(
                       type,
                       type,
                       type.GetMembers()
                           .Where(x => x.IsStatic && x.DeclaredAccessibility == Accessibility.Public
                           )
                           .ToArray(),
                       targetName
                   )
                   ?? "";
        }

        #endregion

        private readonly GetMethodDeclarationDelegate _getMethodDeclaration;
        private readonly GetMethodCommentDelegate     _getMethodComment;
        private readonly string                       _extraMethodBaseName;
        private readonly MethodReturnType             _returnType;
        private readonly bool                         _addBeforeMethod;
        private readonly bool                         _addAfterMethod;
        private readonly bool                         _swapSourceAndTarget;
        private readonly bool                         _useSecondTypeFromAttribute;
        private readonly string                       _paramName;
        private readonly string                       _param2Name;
        private readonly string                       _sourceName;
        private readonly string                       _targetName;
        private readonly string                       _transformStatic;

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
        /// <param name="useSecondTypeFromAttribute"></param>
        public CopyCodeGenerator(
            GetMethodDeclarationDelegate getMethodDeclaration,
            GetMethodCommentDelegate     getMethodComment,
            string                       extraMethodBaseName,
            MethodReturnType             returnType,
            bool                         addBeforeMethod,
            bool                         addAfterMethod             = true,
            bool                         swapSourceAndTarget        = false,
            bool                         useSecondTypeFromAttribute = false
        )
        {
            _getMethodDeclaration       = getMethodDeclaration;
            _getMethodComment           = getMethodComment;
            _extraMethodBaseName        = extraMethodBaseName;
            _returnType                 = returnType;
            _addBeforeMethod            = addBeforeMethod;
            _addAfterMethod             = addAfterMethod;
            _swapSourceAndTarget        = swapSourceAndTarget;
            _useSecondTypeFromAttribute = useSecondTypeFromAttribute;
            if (_useSecondTypeFromAttribute)
            {
                _swapSourceAndTarget = false;
                _paramName           = "source";
                _param2Name          = "target";
                _sourceName          = "source";
                _targetName          = "target";
                _transformStatic     = "";
            }
            else
            {
                _paramName       = _swapSourceAndTarget ? "target" : "source";
                _param2Name      = "this";
                _sourceName      = _swapSourceAndTarget ? "this" : _paramName;
                _targetName      = _swapSourceAndTarget ? _paramName : "this";
                _transformStatic = "static ";
            }
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

                var targetMembers = GetValues(
                    classSymbol,
                    true,
                    _swapSourceAndTarget,
                    _swapSourceAndTarget || _returnType == MethodReturnType.Constructor
                );

                foreach (var (sourceSym, targetSym) in classDeclarationSet.set.ObjectNames
                                                                          .OrderBy(x => x.source.ContainingNamespace
                                                                              .Name
                                                                          )
                                                                          .ThenBy(x => x.source.Name)
                                                                          .ThenBy(x => x.target.ContainingNamespace.Name
                                                                          )
                                                                          .ThenBy(x => x.target.Name))
                {
                    string                           sourceClassName;
                    string                           targetClassName = className;
                    IReadOnlyCollection<ValueSymbol> members;

                    if (_useSecondTypeFromAttribute)
                    {
                        sourceClassName = $"{sourceSym.ContainingNamespace}.{sourceSym.Name}";
                        targetClassName = $"{targetSym.ContainingNamespace}.{targetSym.Name}";

                        // We will look at the source type and the host type.
                        // Source members can be read-only.
                        // We can only touch public properties and fields in the source type.
                        // We can use non-public methods in the host type that take a single parameter of the source type.
                        // This allows us to define mapping where needed.
                        var sourceMembers = GetValues(sourceSym, false, true, true)
                                            .Concat(GetValues(classSymbol, true, true, true, sourceSym, false, false))
                                            .ToArray();

                        // target members must be writable.
                        targetMembers = GetValues(targetSym, false, false, false);

                        members = targetMembers
                                  .Where(x => x.IsField || x.IsProperty)
                                  // Distinct by name/alternate name
                                  .Distinct(ValueSymbol.Comparer)
                                  // Parameterized methods are the most preferred.
                                  // Then non-parameterized methods.
                                  // Then properties.
                                  // Then fields.
                                  .OrderBy(x => x.Preference)
                                  // Where we can find a match in the other object.
                                  .Where(x => x.SelectOther(sourceMembers))
                                  .ToArray();
                    }
                    else
                    {
                        if (SymbolEqualityComparer.Default.Equals(classSymbol, sourceSym))
                        {
                            // same class, copy 1-1
                            sourceClassName = className;
                            members         = targetMembers;
                        }
                        else if (_swapSourceAndTarget)
                        {
                            sourceClassName = $"{sourceSym.ContainingNamespace}.{sourceSym.Name}";
                            // copying to a type, we can only touch public writable properties and fields.
                            var sourceMembers = GetValues(sourceSym, false, false, false);

                            // and we'll use the members from the "source" since that is where we are copying to.
                            members = sourceMembers
                                      .Where(x => x.IsField || x.IsProperty)
                                      // distinct by name/alternate name
                                      .Distinct(ValueSymbol.Comparer)
                                      // lowest to highest, prefer fields over properties.
                                      .OrderBy(x => x.Preference)
                                      // where we can find a match in the other object.
                                      .Where(x => x.SelectOther(targetMembers))
                                      .ToArray();
                        }
                        else
                        {
                            sourceClassName = $"{sourceSym.ContainingNamespace}.{sourceSym.Name}";
                            // copying from a type, we can use any readable property or field.
                            var sourceMembers = GetValues(sourceSym, false, true, true);
                            // and we'll use the members from the "target" since that is where we are copying to.
                            members = targetMembers
                                      .Where(x => x.IsField || x.IsProperty)
                                      // distinct by name/alternate name
                                      .Distinct(ValueSymbol.Comparer)
                                      // lowest to highest, prefer fields over properties.
                                      .OrderBy(x => x.Preference)
                                      // where we can find a match in the other object.
                                      .Where(x => x.SelectOther(sourceMembers))
                                      .ToArray();
                        }
                    }

                    var useInitPassthroughs = addInitPassthroughs && !string.Equals(sourceClassName, targetClassName);

                    var sourceClassRef
                        = _swapSourceAndTarget && sourceSym.IsValueType
                              ? "ref "
                              : "";

                    var targetClassRef
                        = !_swapSourceAndTarget && _useSecondTypeFromAttribute && targetSym.IsValueType
                              ? "ref "
                              : "";

                    var declaration = _getMethodDeclaration(
                        targetClassRef + targetClassName,
                        sourceClassRef + sourceClassName,
                        _paramName,
                        _param2Name,
                        classDeclarationSet.set
                    );
                    var comment = _getMethodComment(targetClassName, sourceClassName, classDeclarationSet.set);


                    var body = new StringBuilder();

                    if (_addBeforeMethod)
                    {
                        if (_returnType == MethodReturnType.Count)
                        {
                            if (_useSecondTypeFromAttribute)
                            {
                                body.AppendFormat(
                                    @"
    /// <summary>
    /// Method to run before the {0} method begins copying values.
    /// </summary>
    partial void Before{0}({3}{1} {2}, {6}{4} {5}, ref int changeCount);
",
                                    _extraMethodBaseName,
                                    sourceClassName,
                                    _paramName,
                                    sourceClassRef,
                                    targetClassName,
                                    _param2Name,
                                    targetClassRef
                                );
                            }
                            else
                            {
                                body.AppendFormat(
                                    @"
    /// <summary>
    /// Method to run before the {0} method begins copying values.
    /// </summary>
    partial void Before{0}({3}{1} {2}, ref int changeCount);
",
                                    _extraMethodBaseName,
                                    sourceClassName,
                                    _paramName,
                                    sourceClassRef
                                );
                            }
                        }
                        else
                        {
                            if (_useSecondTypeFromAttribute)
                            {
                                body.AppendFormat(
                                    @"
    /// <summary>
    /// Method to run before the {0} method begins copying values.
    /// </summary>
    partial void Before{0}({3}{1} {2}, {6}{4} {5});
",
                                    _extraMethodBaseName,
                                    sourceClassName,
                                    _paramName,
                                    sourceClassRef,
                                    targetClassName,
                                    _param2Name,
                                    targetClassRef
                                );
                            }
                            else
                            {
                                body.AppendFormat(
                                    @"
    /// <summary>
    /// Method to run before the {0} method begins copying values.
    /// </summary>
    partial void Before{0}({3}{1} {2});
",
                                    _extraMethodBaseName,
                                    sourceClassName,
                                    _paramName,
                                    sourceClassRef
                                );
                            }
                        }
                    }

                    if (_addAfterMethod)
                    {
                        if (_returnType == MethodReturnType.Count)
                        {
                            if (_useSecondTypeFromAttribute)
                            {
                                body.AppendFormat(
                                    @"
    /// <summary>
    /// Method to run after the {0} method finishes copying values.
    /// </summary>
    partial void After{0}({3}{1} {2}, {6}{4} {5}, ref int changeCount);
",
                                    _extraMethodBaseName,
                                    sourceClassName,
                                    _paramName,
                                    sourceClassRef,
                                    targetClassName,
                                    _param2Name,
                                    targetClassRef
                                );
                            }
                            else
                            {
                                body.AppendFormat(
                                    @"
    /// <summary>
    /// Method to run after the {0} method finishes copying values.
    /// </summary>
    partial void After{0}({3}{1} {2}, ref int changeCount);
",
                                    _extraMethodBaseName,
                                    sourceClassName,
                                    _paramName,
                                    sourceClassRef
                                );
                            }
                        }
                        else
                        {
                            if (_useSecondTypeFromAttribute)
                            {
                                body.AppendFormat(
                                    @"
    /// <summary>
    /// Method to run after the {0} method finishes copying values.
    /// </summary>
    partial void After{0}({3}{1} {2}, {6}{4} {5});
",
                                    _extraMethodBaseName,
                                    sourceClassName,
                                    _paramName,
                                    sourceClassRef,
                                    targetClassName,
                                    _param2Name,
                                    targetClassRef
                                );
                            }
                            else
                            {
                                body.AppendFormat(
                                    @"
    /// <summary>
    /// Method to run after the {0} method finishes copying values.
    /// </summary>
    partial void After{0}({3}{1} {2});
",
                                    _extraMethodBaseName,
                                    sourceClassName,
                                    _paramName,
                                    sourceClassRef
                                );
                            }
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
                    if (_returnType == MethodReturnType.Count)
                    {
                        if (_useSecondTypeFromAttribute)
                        {
                            body.AppendFormat(
                                @"
        if (ReferenceEquals(null, {0})) throw new ArgumentNullException();
        if (ReferenceEquals(null, {1})) throw new ArgumentNullException();
        if (ReferenceEquals({1}, {0})) return 0;
        var changeCount = 0;",
                                _paramName,
                                _param2Name
                            );
                        }
                        else
                        {
                            body.AppendFormat(
                                @"
        if (ReferenceEquals(null, {0})) throw new ArgumentNullException();
        if (ReferenceEquals({1}, {0})) return 0;
        var changeCount = 0;",
                                _paramName,
                                _param2Name
                            );
                        }
                    }
                    else if (_returnType == MethodReturnType.This)
                    {
                        if (_useSecondTypeFromAttribute)
                        {
                            body.AppendFormat(
                                @"
        if (ReferenceEquals(null, {0})) throw new ArgumentNullException();
        if (ReferenceEquals(null, {1})) throw new ArgumentNullException();
        if (ReferenceEquals({1}, {0})) return {1};",
                                _paramName,
                                _param2Name
                            );
                        }
                        else
                        {
                            body.AppendFormat(
                                @"
        if (ReferenceEquals(null, {0})) throw new ArgumentNullException();
        if (ReferenceEquals({1}, {0})) return {1};",
                                _paramName,
                                _param2Name
                            );
                        }
                    }
                    else
                    {
                        // if the method isn't returning, it would be a constructor, so no need for a check against this.
                        body.AppendFormat(
                            @"
        if (ReferenceEquals(null, {0})) throw new ArgumentNullException();",
                            _paramName
                        );
                    }

                    if (_addBeforeMethod)
                    {
                        if (_returnType == MethodReturnType.Count)
                        {
                            if (_useSecondTypeFromAttribute)
                            {
                                body.AppendFormat(
                                    @"
        Before{0}({2}{1}, {4}{3}, ref changeCount);",
                                    _extraMethodBaseName,
                                    _paramName,
                                    sourceClassRef,
                                    _param2Name,
                                    targetClassRef
                                );
                            }
                            else
                            {
                                body.AppendFormat(
                                    @"
        Before{0}({2}{1}, ref changeCount);",
                                    _extraMethodBaseName,
                                    _paramName,
                                    sourceClassRef
                                );
                            }
                        }
                        else
                        {
                            if (_useSecondTypeFromAttribute)
                            {
                                body.AppendFormat(
                                    @"
        Before{0}({2}{1}, {3}{4});",
                                    _extraMethodBaseName,
                                    _paramName,
                                    sourceClassRef,
                                    _param2Name,
                                    targetClassRef
                                );
                            }
                            else
                            {
                                body.AppendFormat(
                                    @"
        Before{0}({2}{1});",
                                    _extraMethodBaseName,
                                    _paramName,
                                    sourceClassRef
                                );
                            }
                        }
                    }

                    // now handle the properties.
                    foreach (var p in members.OrderBy(x => x.AlternateName))
                    {
                        var sourceProp = p.Other ?? p;
                        var targetProp = p;

                        if (!transforms.ContainsKey(targetProp.AlternateName))
                        {
                            transforms[targetProp.AlternateName] = $@"
    /// <summary>
    /// Transforms the {targetProp.AlternateName} value before assigning the value to the target.
    /// </summary>
    {_transformStatic}partial void {_extraMethodBaseName}Transform_{targetProp.AlternateName}(ref {targetProp.Type} value);";
                        }

                        // passthroughs will only be used against a "source" parameter.
                        if (useInitPassthroughs)
                        {
                            var getSource = sourceProp.ToGetString("source");
                            transforms[$"~{targetProp.AlternateName}_{sourceClassName}"] = $@"
    /// <summary>
    /// Transforms the {targetProp.AlternateName} value and returns the new value.
    /// </summary>
    static {targetProp.Type} PassthroughTransform_{targetProp.AlternateName}({sourceClassName} source)
    {{
        if (ReferenceEquals(null, source)) throw new ArgumentNullException();
        var value = {getSource};
        {_extraMethodBaseName}Transform_{targetProp.AlternateName}(ref value);
        return value;
    }}";
                        }

                        // property setting for parameterized properties is handled via the passthroughs only.
                        if (useInitPassthroughs && recParamList.Contains(targetProp.AlternateName)) continue;
                        
                        var propParams = new PropertyParameters(
                            classSymbol,
                            targetProp,
                            sourceProp,
                            _extraMethodBaseName,
                            _sourceName,
                            _targetName,
                            GetNullDefault
                        );

                        var fmt =
                            (_returnType == MethodReturnType.Count)
                                ? targetProp.Type.IsValueType
                                      ? (propParams.IsSourceNullableValueType && propParams.IsTargetNonNullable)
                                            ? propParams.SetNonNullableValueTypeWithCount
                                            : propParams.SetValueTypeWithCount
                                      : propParams.IsTargetNonNullable
                                          ? propParams.SetNonNullableReferenceTypeWithCount
                                          : propParams.SetReferenceTypeWithCount
                                : propParams.IsTargetNonNullable &&
                                  (
                                      !targetProp.Type.IsValueType ||
                                      propParams.IsSourceNullableValueType
                                  )
                                    ? propParams.SetNonNullableValue
                                    : propParams.SetValue;

                        body.AppendFormat(fmt.Format, fmt.GetArguments());
                    }

                    if (_addAfterMethod)
                    {
                        if (_returnType == MethodReturnType.Count)
                        {
                            if (_useSecondTypeFromAttribute)
                            {
                                body.AppendFormat(
                                    @"
        After{0}({2}{1}, {4}{3}, ref changeCount);",
                                    _extraMethodBaseName,
                                    _paramName,
                                    sourceClassRef,
                                    _param2Name,
                                    targetClassRef
                                );
                            }
                            else
                            {
                                body.AppendFormat(
                                    @"
        After{0}({2}{1}, ref changeCount);",
                                    _extraMethodBaseName,
                                    _paramName,
                                    sourceClassRef
                                );
                            }
                        }
                        else
                        {
                            if (_useSecondTypeFromAttribute)
                            {
                                body.AppendFormat(
                                    @"
        After{0}({2}{1}, {4}{3});",
                                    _extraMethodBaseName,
                                    _paramName,
                                    sourceClassRef,
                                    _param2Name,
                                    targetClassRef
                                );
                            }
                            else
                            {
                                body.AppendFormat(
                                    @"
        After{0}({2}{1});",
                                    _extraMethodBaseName,
                                    _paramName,
                                    sourceClassRef
                                );
                            }
                        }
                    }

                    switch (_returnType)
                    {
                        case MethodReturnType.This:
                            body.Append("\n        return this;");
                            break;
                        case MethodReturnType.Count:
                            body.Append("\n        return changeCount;");
                            break;
                        case MethodReturnType.Source:
                            body.AppendFormat("\n        return {0};", _sourceName);
                            break;
                        case MethodReturnType.Target:
                            body.AppendFormat("\n        return {0};", _targetName);
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
