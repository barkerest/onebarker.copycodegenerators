using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;

namespace OneBarker.CopyCodeGenerators
{
    public class ValueSymbol
    {
        private static readonly SymbolEqualityComparer Comparer = SymbolEqualityComparer.Default;

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
        
        [CanBeNull] public ValueSymbol Other { get; private set; }

        public ValueSymbol(IPropertySymbol symbol)
        {
            Symbol        = symbol      ?? throw new ArgumentNullException(nameof(symbol));
            Type          = symbol.Type ?? throw new ArgumentException("Symbol type is null.", nameof(symbol));
            Name          = symbol.Name ?? throw new ArgumentException("Symbol name is null.", nameof(symbol));
            AlternateName = Name;
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
            AlternateName = Name.StartsWith("_") ? Name.Substring(1) : Name;
            IsField       = true;
            CanRead       = true;
            CanWrite      = !symbol.IsReadOnly;
            IsInitOnly    = symbol.IsReadOnly;
            Preference    = 1;
        }

        public ValueSymbol(IMethodSymbol symbol)
        {
            Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
            if (symbol.ReturnsVoid) throw new ArgumentException("Method must have a return type.", nameof(symbol));
            Type = symbol.ReturnType ?? throw new ArgumentException("Symbol type is null.", nameof(symbol));
            if (!symbol.Parameters.IsEmpty)
                throw new ArgumentException("Method must have no parameters.", nameof(symbol));
            if (symbol.IsStatic) throw new ArgumentException("Method must not be static.", nameof(symbol));
            Name          = symbol.Name ?? throw new ArgumentException("Symbol name is null.", nameof(symbol));
            AlternateName = Name.StartsWith("_") 
                                ? Name.Substring(1) 
                                : Name.StartsWith("Get_", StringComparison.OrdinalIgnoreCase) 
                                    ? Name.Substring(4) 
                                    : Name.StartsWith("Default_", StringComparison.OrdinalIgnoreCase)
                                    ? Name.Substring(8)
                                    : Name;
            IsField    = false;
            IsProperty = false;
            CanRead    = true;
            CanWrite   = false;
            IsInitOnly = false;
            
            // Default gets the lowest preference.
            Preference = Name.StartsWith("Default_", StringComparison.OrdinalIgnoreCase) ? 0 : 3;
        }

        public bool SelectOther(IEnumerable<ValueSymbol> others)
        {
            Other = others
                    // same name or alternate name
                    .Where(x => x.Equals(this))
                    // highest to lowest, prefer methods over properties, properties over fields.
                    .OrderByDescending(x => x.Preference)
                    .FirstOrDefault();
            return !ReferenceEquals(Other, null);
        }
        
        
        protected bool Equals(ValueSymbol other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (!string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(Name, other.AlternateName, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(AlternateName, other.Name, StringComparison.OrdinalIgnoreCase))
            {
                // name must match, or name and alternate name must match.  Matching is case-insensitive (camelCase == CamelCase)
                return false;
            }

            if (!Comparer.Equals(Type, other.Type)) return false;
            return true;
        }

        public override string ToString()
        {
            if (IsField || IsProperty) return Name;
            return Name + "()";
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj is ValueSymbol other) return Equals(other);
            return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Comparer.GetHashCode(Type);
                hashCode = (hashCode * 397) ^ Name.ToUpper().GetHashCode();
                hashCode = (hashCode * 397) ^ AlternateName.ToUpper().GetHashCode();
                return hashCode;
            }
        }
    }
}
