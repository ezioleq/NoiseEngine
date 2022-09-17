﻿namespace NoiseEngine.CodeGenerators.Interop.RustMarshalling;

internal class SpanMarshaller : RustMarshaller {

    public override string MarshalledType => "System.Span";
    public override string UnmarshalledType => "NoiseEngine.Interop.InteropSpan";
    public override bool IsAdvanced => true;

    public override string Marshall(string parameterName, out string newParameterName) {
        newParameterName = CreateUniqueVariableName();
        string finalType = $"{UnmarshalledType}<{GenericRawString}>";
        string a = CreateUniqueVariableName();

        return @$"
            fixed ({GenericRawString}* {a} = {parameterName}) {{
                {finalType} {newParameterName} = new {finalType}({a}, {parameterName}.Length);

                {MarshallingContinuation}
            }}
        ";
    }

    public override string Unmarshall(string parameterName, out string newParameterName) {
        newParameterName = CreateUniqueVariableName();
        return $"{MarshalledType}<{GenericRawString}> {newParameterName} = {parameterName};";
    }

}
