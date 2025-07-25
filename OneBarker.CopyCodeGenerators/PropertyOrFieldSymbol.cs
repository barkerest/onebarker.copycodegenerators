using System;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;

namespace OneBarker.CopyCodeGenerators
{
    public class PropertyOrFieldSymbol
    {
        private static readonly SymbolEqualityComparer Comparer = SymbolEqualityComparer.Default;
        
        [NotNull]
        public ISymbol         Symbol        { get; }
        
        [NotNull]
        public ITypeSymbol     Type          { get; }
        
        [NotNull]
        public string          Name          { get; }
        
        [NotNull]
        public string          AlternateName { get; }

        public bool IsField    { get; }
        public bool IsProperty { get; }
        
        public bool CanRead { get; }
        
        public bool CanWrite { get; }
        
        public bool IsInitOnly { get; }
        
        public PropertyOrFieldSymbol(IPropertySymbol symbol)
        {
            Symbol        = symbol      ?? throw new ArgumentNullException(nameof(symbol));
            Type          = symbol.Type ?? throw new ArgumentException("Symbol type is null.", nameof(symbol));
            Name          = symbol.Name ?? throw new ArgumentException("Symbol name is null.", nameof(symbol));
            AlternateName = Name;
            IsProperty    = true;
            CanRead       = symbol.GetMethod != null;
            CanWrite      = symbol.SetMethod != null;
            IsInitOnly    = symbol.SetMethod?.IsInitOnly ?? false;
        }

        public PropertyOrFieldSymbol(IFieldSymbol symbol)
        {
            Symbol        = symbol      ?? throw new ArgumentNullException(nameof(symbol));
            Type          = symbol.Type ?? throw new ArgumentException("Symbol type is null.", nameof(symbol));
            Name          = symbol.Name ?? throw new ArgumentException("Symbol name is null.", nameof(symbol));
            AlternateName = Name.StartsWith("_") ? Name.Substring(1) : Name;
            IsField       = true;
            CanRead       = true;
            CanWrite      = !symbol.IsReadOnly;
            IsInitOnly    = symbol.IsReadOnly;
        }

        protected bool Equals(PropertyOrFieldSymbol other)
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

        
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj is PropertyOrFieldSymbol other) return Equals(other);
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
