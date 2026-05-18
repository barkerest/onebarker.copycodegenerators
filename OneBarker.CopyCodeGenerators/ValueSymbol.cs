using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;

namespace OneBarker.CopyCodeGenerators
{
    public class ValueSymbol
    {
        internal class EqualityComparer : IEqualityComparer<ValueSymbol>
        {
            public bool Equals(ValueSymbol x, ValueSymbol y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x is null)
                {
                    return false;
                }

                if (y is null)
                {
                    return false;
                }

                if (x.GetType() != y.GetType())
                {
                    return false;
                }

                return string.Equals(x.AlternateName, y.AlternateName, StringComparison.OrdinalIgnoreCase)
                       && SymbolComparer.Equals(x.Type, y.Type);
            }

            public int GetHashCode(ValueSymbol obj)
            {
                return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.AlternateName);
            }
        }
        
        public static readonly IEqualityComparer<ValueSymbol> Comparer = new EqualityComparer();
        private static readonly SymbolEqualityComparer SymbolComparer = SymbolEqualityComparer.Default;
        
        
        [NotNull]
        public ISymbol Symbol { get; }

        [NotNull]
        public ITypeSymbol Type { get; }

        [NotNull]
        public string Name { get; }

        [NotNull]
        public string AlternateName { get; }

        public bool IsField    { get; }
        public bool IsProperty { get; }

        // Methods carry the highest preference, followed by properties, and fields at the bottom.
        // This allows for code to handle logic before falling back on the raw data field.
        public int Preference { get; }

        public bool CanRead { get; }

        public bool CanWrite { get; }

        public bool IsInitOnly { get; }

        public bool TakesSourceParam { get; }

        [CanBeNull]
        public ValueSymbol Other { get; private set; }

        private static string FixName(string name, params string[] prefixes)
        {
            foreach (var prefix in prefixes)
            {
                if (name.Length > prefix.Length && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(prefix.Length);
                    if (name.Length == 1) return name.ToUpper();
                    return name.Substring(0, 1).ToUpper() + name.Substring(1);
                }
            }

            if (name.Length == 1) return name.ToUpper();
            return name.Substring(0, 1).ToUpper() + name.Substring(1);
        }

        public ValueSymbol(IPropertySymbol symbol)
        {
            Symbol        = symbol      ?? throw new ArgumentNullException(nameof(symbol));
            Type          = symbol.Type ?? throw new ArgumentException("Symbol type is null.", nameof(symbol));
            Name          = symbol.Name ?? throw new ArgumentException("Symbol name is null.", nameof(symbol));
            AlternateName = FixName(Name);
            IsProperty    = true;
            CanRead       = symbol.GetMethod != null;
            CanWrite      = symbol.SetMethod != null;
            IsInitOnly    = symbol.SetMethod?.IsInitOnly ?? false;
            Preference    = 2;
        }

        
        public ValueSymbol(IFieldSymbol symbol)
        {
            Symbol        = symbol      ?? throw new ArgumentNullException(nameof(symbol));
            Type          = symbol.Type ?? throw new ArgumentException("Symbol type is null.", nameof(symbol));
            Name          = symbol.Name ?? throw new ArgumentException("Symbol name is null.", nameof(symbol));
            AlternateName = FixName(Name, "_");
            IsField    = true;
            CanRead    = true;
            CanWrite   = !symbol.IsReadOnly;
            IsInitOnly = symbol.IsReadOnly;
            Preference = 3;
        }

        public ValueSymbol(IMethodSymbol symbol)
        {
            Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
            if (symbol.IsStatic) throw new ArgumentException("Method must not be static.", nameof(symbol));

            // TODO: If we support setters in the future, this block needs updated accordingly!
            if (symbol.ReturnsVoid) throw new ArgumentException("Method must have a return type.", nameof(symbol));
            Type = symbol.ReturnType ?? throw new ArgumentException("Symbol type is null.", nameof(symbol));
            if (symbol.Parameters.Length > 1)
                throw new ArgumentException("Method must have zero or one parameters.", nameof(symbol));


            Name          = symbol.Name ?? throw new ArgumentException("Symbol name is null.", nameof(symbol));
            AlternateName = FixName(Name, "_", "Get_", "Default_");
            IsField    = false;
            IsProperty = false;
            CanRead    = true;
            CanWrite   = false;
            IsInitOnly = false;

            // Default gets the lowest preference.
            Preference = Name.StartsWith("Default_", StringComparison.OrdinalIgnoreCase) ? 5 : 1;

            // a parameterized method is more preferred.
            TakesSourceParam = symbol.Parameters.Length == 1;
            if (TakesSourceParam) Preference -= 1;
        }

        public bool SelectOther(IEnumerable<ValueSymbol> others)
        {
            Other = others
                    // same name or alternate name
                    .Where(x => x.Equals(this))
                    // prefer methods over properties, properties over fields.
                    .OrderBy(x => x.Preference)
                    .FirstOrDefault();
            return !ReferenceEquals(Other, null);
        }
        
        public string ToGetString(string sourceName)
        {
            // the first two cases are the normal "pulling from the source object directly" variants.
            if (IsField || IsProperty) return $"{sourceName}.{Name}";
            if (!TakesSourceParam) return $"{sourceName}.{Name}()";

            // the final case is the parameterized version that is targeted against "this".
            return $"{Name}({sourceName})";
        }

        public string ToSetString(string targetName, string valueName)
        {
            // like above, the first two push directly to the target.
            if (IsField || IsProperty) return $"{targetName}.{Name} = {valueName}";

            // TODO: Enable setters?
            //       This has been added here for completeness, but we do not currently look for symbols to set the value. 
            if (!TakesSourceParam) return $"{targetName}.{Name}({valueName})";

            // and the final is the parameterized version targeted against "this".
            return $"{Name}({targetName}, {valueName})";
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj is ValueSymbol other) return Comparer.Equals(this, other);
            return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = SymbolComparer.GetHashCode(Type);
                hashCode = (hashCode * 397) ^ Name.ToUpper().GetHashCode();
                hashCode = (hashCode * 397) ^ AlternateName.ToUpper().GetHashCode();
                return hashCode;
            }
        }
    }
}
