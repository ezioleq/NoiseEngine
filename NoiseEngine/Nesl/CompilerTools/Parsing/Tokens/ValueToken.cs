﻿using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace NoiseEngine.Nesl.CompilerTools.Parsing.Tokens;

internal record ValueToken(
    OperatorType LeftOperator, IValueContent Value, OperatorType? RightOperator, ValueToken? NextValue
) : IParserToken<ValueToken>, IValueContent {

    public OperatorType Operator { get; private set; }

    public bool IsIgnored => false;
    public int Priority => 0;

    public CodePointer Pointer => Value.Pointer;

    public static bool Parse(
        TokenBuffer buffer, CompilationErrorMode errorMode, [NotNullWhen(true)] out ValueToken? result,
        out CompilationError error
    ) {
        OperatorToken? leftOperator = null;
        if (
            OperatorToken.Parse(buffer, errorMode, out OperatorToken tempOperator, out _) &&
            !tempOperator.IsAssigment && (
                tempOperator.Type == OperatorType.Increment ||
                tempOperator.Type == OperatorType.Decrement ||
                tempOperator.Type == OperatorType.Subtraction ||
                tempOperator.Type == OperatorType.Negation ||
                tempOperator.Type == OperatorType.Bitwise
            )
        ) {
            leftOperator = tempOperator;
        } else {
            buffer.Index--;
        }

        if (!buffer.TryReadNext(out Token token)) {
            result = null;
            error = new CompilationError(token, CompilationErrorType.ExpectedValue);
            return false;
        }

        IValueContent value;
        int index;
        if (token.Type == TokenType.Word) {
            bool isNew = token.Value == "new";
            if (!isNew)
                buffer.Index--;

            // Get current expression.
            if (!TryGetIdentifierWithRoundBrackets(
                buffer, errorMode, out error, out TypeIdentifierToken identifier, out RoundBracketsToken? roundBrackets
            )) {
                result = null;
                return false;
            }

            CurlyBracketsToken? curlyBrackets = null;
            if (isNew) {
                index = buffer.Index;
                if (CurlyBracketsToken.Parse(buffer, errorMode, out CurlyBracketsToken tempCurlyBrackets, out _))
                    curlyBrackets = tempCurlyBrackets;
                else
                    buffer.Index = index;
            }

            if (!TryGetIndexer(buffer, errorMode, out error, out ValueToken? indexer)) {
                result = null;
                return false;
            }

            List<ExpressionValueContent> expressions = new List<ExpressionValueContent> {
                new ExpressionValueContent(isNew, identifier, roundBrackets, curlyBrackets, indexer)
            };

            // Get next expressions
            while (buffer.TryReadNext(out token)) {
                if (token.Type == TokenType.Dot) {
                    if (!TryGetIdentifierWithRoundBrackets(
                        buffer, errorMode, out error, out identifier, out roundBrackets
                    )) {
                        result = null;
                        return false;
                    }
                } else if (token.Type != TokenType.SquareBracketOpening) {
                    buffer.Index--;
                    break;
                }

                if (!TryGetIndexer(buffer, errorMode, out error, out indexer)) {
                    result = null;
                    return false;
                }

                expressions.Add(new ExpressionValueContent(false, identifier, roundBrackets, null, indexer));
            }

            // Create value
            value = new ExpressionValueContentContainer(expressions);
        } else if (token.Type == TokenType.RoundBracketOpening) {
            index = buffer.Index;
            if (Parse(buffer, errorMode, out ValueToken? innerValue, out error)) {
                value = innerValue;
            } else {
                result = default;
                return false;
            }

            if (token.Length <= 1) {
                result = null;
                error = new CompilationError(token, CompilationErrorType.ExpectedClosingRoundBracket);
                return false;
            }

            buffer.Index = index + token.Length - 1;
        } else {
            result = null;
            error = new CompilationError(token, CompilationErrorType.ExpectedValue);
            return false;
        }

        OperatorToken? rightOperator = null;
        if (
            OperatorToken.Parse(buffer, errorMode, out tempOperator, out _) &&
            !tempOperator.IsAssigment && (
                tempOperator.Type == OperatorType.Increment ||
                tempOperator.Type == OperatorType.Decrement
            )
        ) {
            rightOperator = tempOperator;
        } else {
            buffer.Index--;
        }

        index = buffer.Index;
        bool hasOperator =
            OperatorToken.Parse(buffer, errorMode, out tempOperator, out _) && !tempOperator.IsAssigment;
        if (!hasOperator)
            buffer.Index = index;

        index = buffer.Index;
        if (Parse(buffer, errorMode, out ValueToken? nextValue, out error)) {
            if (hasOperator)
                nextValue.Operator = tempOperator.Type;
        } else if (hasOperator) {
            result = null;
            return false;
        } else {
            buffer.Index = index;
        }

        if (hasOperator || nextValue is null) {
            result = new ValueToken(
                leftOperator?.Type ?? OperatorType.None, value, rightOperator?.Type ?? OperatorType.None, nextValue
            );
            error = default;
            return true;
        } else {
            if (value is not ValueToken cast) {
                result = default;
                error = new CompilationError(
                    value.Pointer, CompilationErrorType.ExpectedExplicitCastNotExpression, ""
                );
                return false;
            }

            if (cast.Value is ExpressionValueContentContainer container && container.Expressions.Count == 1) {
                ExpressionValueContent c = container.Expressions[0];
                if (!c.IsNew && !c.RoundBrackets.HasValue && c.Indexer is null && !c.CurlyBrackets.HasValue) {
                    nextValue.Operator = OperatorType.ExplicitCast;
                    result = new ValueToken(
                        OperatorType.None, new CastValue(c.Identifier!.Value), OperatorType.None, nextValue
                    );
                    error = default;
                    return true;
                }
            }

            result = default;
            error = new CompilationError(value.Pointer, CompilationErrorType.ExpectedExplicitCastNotValue, "");
            return false;
        }
    }

    private static bool TryGetIdentifierWithRoundBrackets(
        TokenBuffer buffer, CompilationErrorMode errorMode, out CompilationError error,
        out TypeIdentifierToken identifier, out RoundBracketsToken? roundBrackets
    ) {
        if (!TypeIdentifierToken.Parse(buffer, errorMode, out identifier, out error)) {
            roundBrackets = null;
            return false;
        }

        int index = buffer.Index;
        roundBrackets = null;
        if (RoundBracketsToken.Parse(buffer, errorMode, out RoundBracketsToken tempRoundBrackets, out _))
            roundBrackets = tempRoundBrackets;
        else
            buffer.Index = index;

        return true;
    }

    private static bool TryGetIndexer(
        TokenBuffer buffer, CompilationErrorMode errorMode, out CompilationError error, out ValueToken? indexer
    ) {
        indexer = null;
        int index = buffer.Index;
        if (buffer.TryReadNext(TokenType.SquareBracketOpening, out Token token)) {
            if (!Parse(buffer, errorMode, out indexer, out error)) {
                error = default;
                return false;
            }

            if (token.Length <= 1) {
                error = new CompilationError(token, CompilationErrorType.ExpectedClosingSquareBracket);
                return false;
            }
        } else {
            buffer.Index = index;
        }

        error = default;
        return true;
    }

}
