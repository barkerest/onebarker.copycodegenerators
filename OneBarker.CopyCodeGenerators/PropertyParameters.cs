using System;
using Microsoft.CodeAnalysis;

namespace OneBarker.CopyCodeGenerators
{
    public class PropertyParameters
    {
        public delegate string GetNullDefault(ITypeSymbol type, INamedTypeSymbol containingClass, string targetName);

        public PropertyParameters(
            INamedTypeSymbol containingClass,
            ValueSymbol      target,
            ValueSymbol      source,
            string           extraMethod,
            string           sourceObjectName,
            string           targetObjectName,
            GetNullDefault   getNullDefault,
            string           changeCountValueName = "changeCount"
        )
        {
            MemberName           = target.AlternateName;
            ExtraMethod          = extraMethod;
            SourceValueName      = $"{sourceObjectName}_{MemberName}";
            TargetValueName      = $"{targetObjectName}_{MemberName}";
            ChangeCountValueName = changeCountValueName;
            SourceObjectName     = sourceObjectName;
            TargetObjectName     = targetObjectName;
            GetSourceValueCode   = source.ToGetString(SourceObjectName);
            GetTargetValueCode   = target.ToGetString(TargetObjectName);
            SetTargetValueCode   = target.ToSetString(TargetObjectName, SourceValueName);
            IsValueType          = target.Type.IsValueType;
            IsTargetNonNullable  = target.Type.NullableAnnotation != NullableAnnotation.Annotated;
            IsSourceNullableValueType = source.Type.IsValueType &&
                                        source.Type.NullableAnnotation ==
                                        NullableAnnotation.Annotated;

            if (IsTargetNonNullable)
            {
                var defVal = getNullDefault(target.Type, containingClass, target.Name);
                if (IsValueType)
                {
                    if (string.IsNullOrEmpty(defVal)) defVal = $"default({target.Type.Name})";
                    SetSourceValueNonNullCode = $"{SourceValueName} = {defVal}";
                }
                else if (string.IsNullOrEmpty(defVal))
                {
                    SetSourceValueNonNullCode =
                        $@"throw new InvalidOperationException(""The source property {source.Name} has a value of null and the type {target.Type.Name} does not have a default/empty value."")";
                }
                else
                {
                    SetSourceValueNonNullCode = $"{SourceValueName} = {defVal}";
                }
            }
            else
            {
                SetSourceValueNonNullCode = "";
            }
        }

        public string MemberName                { get; }
        public string ExtraMethod               { get; }
        public string SourceValueName           { get; }
        public string TargetValueName           { get; }
        public string ChangeCountValueName      { get; }
        public string SourceObjectName          { get; }
        public string TargetObjectName          { get; }
        public string GetSourceValueCode        { get; }
        public string GetTargetValueCode        { get; }
        public string SetTargetValueCode        { get; }
        public string SetSourceValueNonNullCode { get; }
        public bool   IsValueType               { get; }
        public bool   IsTargetNonNullable       { get; }
        public bool   IsSourceNullableValueType { get; }


        public FormattableString SetNonNullableValueTypeWithCount
            => $@"
        var {TargetValueName} = {GetTargetValueCode};
        var {SourceValueName} = {GetSourceValueCode};
        if (!{SourceValueName}.HasValue) {SetSourceValueNonNullCode};
        {ExtraMethod}Transform_{MemberName}(ref {SourceValueName});
        if (!{SourceValueName}.HasValue) {SetSourceValueNonNullCode};
        if (!{TargetValueName}.Equals({SourceValueName})) {{
            {SetTargetValueCode};
            {ChangeCountValueName}++;
        }}";

        public FormattableString SetValueTypeWithCount
            => $@"
        var {TargetValueName} = {GetTargetValueCode};
        var {SourceValueName} = {GetSourceValueCode};
        {ExtraMethod}Transform_{MemberName}(ref {SourceValueName});
        if (!{TargetValueName}.Equals({SourceValueName})) {{
            {SetTargetValueCode};
            {ChangeCountValueName}++;
        }}";

        public FormattableString SetNonNullableReferenceTypeWithCount
            => $@"
        var {TargetValueName} = {GetTargetValueCode};
        var {SourceValueName} = {GetSourceValueCode};
        if (ReferenceEquals(null, {SourceValueName})) {SetSourceValueNonNullCode};
        {ExtraMethod}Transform_{MemberName}(ref {SourceValueName});
        if (ReferenceEquals(null, {SourceValueName})) {SetSourceValueNonNullCode};
        if (!ReferenceEquals({TargetValueName}, {SourceValueName}) && (ReferenceEquals(null, {TargetValueName}) || !{TargetValueName}.Equals({SourceValueName}))) {{
            {SetTargetValueCode};
            {ChangeCountValueName}++;
        }}";

        public FormattableString SetReferenceTypeWithCount
            => $@"
        var {TargetValueName} = {GetTargetValueCode};
        var {SourceValueName} = {GetSourceValueCode};
        {ExtraMethod}Transform_{MemberName}(ref {SourceValueName});
        if (!ReferenceEquals({TargetValueName}, {SourceValueName}) && (ReferenceEquals(null, {TargetValueName}) || (!ReferenceEquals(null, {TargetValueName}) && !{TargetValueName}.Equals({SourceValueName})))) {{
            {SetTargetValueCode};
            {ChangeCountValueName}++;
        }}";


        public FormattableString SetNonNullableValue     
            => $@"
        var {SourceValueName} = {GetSourceValueCode};
        if (ReferenceEquals(null, {SourceValueName})) {SetSourceValueNonNullCode};
        {ExtraMethod}Transform_{MemberName}(ref {SourceValueName});
        if (ReferenceEquals(null, {SourceValueName})) {SetSourceValueNonNullCode};
        {SetTargetValueCode};";
        
        
        public FormattableString SetValue                            
            => $@"
        var {SourceValueName} = {GetSourceValueCode};
        {ExtraMethod}Transform_{MemberName}(ref {SourceValueName});
        {SetTargetValueCode};";
        
    }
}
