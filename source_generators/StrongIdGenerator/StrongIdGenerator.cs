﻿using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StrongIdGenerator;

[Generator]
public sealed class StrongIdGenerator : IIncrementalGenerator
{
    private const string GeneratedCodeAttribute = """[global::System.CodeDom.Compiler.GeneratedCodeAttribute("Astro.StrongIdGenerator", "2.0.0")]""";

    private const string StrongIdHelperSourceCode =
        $$"""
          // <auto-generated/>
          #nullable enable

          namespace Astro.Generated;

          {{GeneratedCodeAttribute}}
          public static class StrongIdHelper<TId, TValue> where TId : struct
          {
              public static string Serialize(TValue value) =>
                  $"{Prefix}_{value?.ToString()?.ToLower() ?? string.Empty}";
          
              public static bool Deserialize(string? value, out TId? id)
              {
                  id = default;
          
                  if (string.IsNullOrWhiteSpace(value))
                      return false;
          
                  var prefix = $"{Prefix}_";
                  if (!value!.StartsWith(prefix))
                      return false;
          
                  var rawValue = value.Substring(prefix.Length);
          
                  try
                  {
                      if (global::System.ComponentModel.TypeDescriptor.GetConverter(typeof(TValue)).ConvertFromString(rawValue) is TValue convertedId)
                      {
                          id = (TId?)global::System.Activator.CreateInstance(typeof(TId), convertedId);
                          return true;
                      }
                  }
                  catch
                  {
                  }
          
                  return false;
              }
          
              private static string ToSnakeCase(string text)
              {
                  if (string.IsNullOrWhiteSpace(text))
                      throw new global::System.ArgumentNullException(nameof(text), "Text was null");
          
                  if (text.Length < 2)
                      return text;
          
                  var sb = new global::System.Text.StringBuilder();
                  sb.Append(char.ToLowerInvariant(text[0]));
          
                  for (var i = 1; i < text.Length; ++i)
                  {
                      var c = text[i];
                      if (char.IsUpper(c))
                      {
                          sb.Append('_');
                          sb.Append(char.ToLowerInvariant(c));
                      }
                      else
                      {
                          sb.Append(c);
                      }
                  }
          
                  return sb.ToString();
              }
          
              private static string Prefix => ToSnakeCase(typeof(TId).Name.Replace("Id", string.Empty));
          }
          """;

    private const string StrongIdAttributeSourceCode =
        $$"""
          // <auto-generated/>
          #nullable enable

          namespace Astro.Generated;

          {{GeneratedCodeAttribute}}
          [global::System.AttributeUsage(global::System.AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
          public sealed class StrongIdAttribute(global::System.Type idType) : global::System.Attribute 
          {
              public global::System.Type IdType { get; init; } = idType;
          }

          """;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(PostInitializationCallback);

        var syntaxProvider = context.SyntaxProvider
            .CreateSyntaxProvider(SyntacticPredicate, SemanticTransform)
            .Where(static type => type.HasValue)
            .Select(EntityStrongIdContext.FromEntityTypeInfo)
            .WithComparer(PartialClassContextEqualityComparer.Instance);

        context.RegisterSourceOutput(syntaxProvider, Execute);
    }

    private static void PostInitializationCallback(IncrementalGeneratorPostInitializationContext context)
    {
        context.AddSource("Astro.Generated.StrongIdHelper.g.cs", StrongIdHelperSourceCode);
        context.AddSource("Astro.Generated.StrongIdAttribute.g.cs", StrongIdAttributeSourceCode);
    }

    private static bool SyntacticPredicate(SyntaxNode node, CancellationToken ct)
    {
        var isRecord = node is RecordDeclarationSyntax { AttributeLists.Count: > 0 };
        var isClass = node is ClassDeclarationSyntax { AttributeLists.Count: > 0 };
        return isRecord || isClass;
    }

    private static (INamedTypeSymbol EntityType, INamedTypeSymbol IdType)? SemanticTransform(GeneratorSyntaxContext context, CancellationToken ct)
    {
        Debug.Assert(context.Node is ClassDeclarationSyntax or RecordDeclarationSyntax);

        TypeDeclarationSyntax candidate = context.Node switch
        {
            ClassDeclarationSyntax => Unsafe.As<ClassDeclarationSyntax>(context.Node),
            RecordDeclarationSyntax => Unsafe.As<RecordDeclarationSyntax>(context.Node),
            _ => throw new Exception("StrongId attribute found on anything that is not a record or a class"),
        };

        ISymbol? symbol = context.SemanticModel.GetDeclaredSymbol(candidate, ct);

        if (symbol is INamedTypeSymbol entityType)
        {
            var strongIdAttribute = context.SemanticModel.Compilation.GetTypeByMetadataName("Astro.Generated.StrongIdAttribute");

            if (TryGetAttributeInfo(candidate, strongIdAttribute, context.SemanticModel, out var idType))
            {
                return (entityType, idType);
            }
        }

        return null;
    }

    private static bool TryGetAttributeInfo(TypeDeclarationSyntax candidate, INamedTypeSymbol? target, SemanticModel semanticModel, out INamedTypeSymbol idType)
    {
        foreach (var attributeList in candidate.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(attribute);
                var symbol = symbolInfo.Symbol;

                if (symbol is not null
                    && SymbolEqualityComparer.Default.Equals(symbol.ContainingSymbol, target)
                    && attribute.ArgumentList is
                    {
                        Arguments.Count: 1,
                    } argumentList)
                {
                    var argument = argumentList.Arguments[0];
                    if (argument.Expression is TypeOfExpressionSyntax typeOf
                        && semanticModel.GetSymbolInfo(typeOf.Type).Symbol is INamedTypeSymbol type)
                    {
                        idType = type;
                        return true;
                    }
                }
            }
        }

        idType = null!;
        return false;
    }

    private static void Execute(SourceProductionContext context, EntityStrongIdContext subject)
    {
        var idClassName = $"{subject.TypeName}Id";
        var qualifiedName = subject.Namespace is null ? subject.TypeName : $"{subject.Namespace}.{subject.TypeName}";
        var idType = subject.IdType;

        // TODO if the idType is guid or ulid, if yes, use empty and newGuid, otherwise, dont implement those?

        var idSource = $$"""
                         // <auto-generated/>

                         #nullable enable

                         namespace Astro.Generated;

                         {{GeneratedCodeAttribute}}
                         public readonly record struct {{idClassName}}({{idType}} Value)
                         {
                             public static {{idClassName}} Empty => new({{idType}}.Empty);
                             public static {{idClassName}} New() => new({{idType}}.NewUlid());
                             public override string ToString() => StrongIdHelper<{{idClassName}}, {{idType}}>.Serialize(Value);
                             public static bool TryParse(string? value, [global::System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out {{idClassName}}? id) => StrongIdHelper<{{idClassName}}, {{idType}}>.Deserialize(value, out id);
                             public static {{idClassName}} Parse(string value) => TryParse(value, out var id) ? id.Value : throw new FormatException("Input string was not in the correct format");
                         }
                         """;
        context.AddSource($"Astro.Generated.{qualifiedName}Id.g.cs", idSource);

        var jsonConverterSource = $$"""
                                    // <auto-generated/>

                                    #nullable enable

                                    namespace Astro.Generated;

                                    {{GeneratedCodeAttribute}}
                                    public sealed class {{idClassName}}ToStringJsonConverter : global::System.Text.Json.Serialization.JsonConverter<{{idClassName}}>
                                    {
                                        public override {{idClassName}} Read(ref global::System.Text.Json.Utf8JsonReader reader, Type typeToConvert, global::System.Text.Json.JsonSerializerOptions options)
                                        {
                                            return {{idClassName}}.Parse(reader.GetString());
                                        }
                                    
                                        public override void Write(global::System.Text.Json.Utf8JsonWriter writer, {{idClassName}} value, global::System.Text.Json.JsonSerializerOptions options)
                                        {
                                            writer.WriteStringValue(value.ToString());
                                        }
                                    }
                                    """;
        context.AddSource($"Astro.Generated.{qualifiedName}JsonIdConverter.g.cs", jsonConverterSource);

        var efCoreConverterSource = $"""
                                 // <auto-generated/>

                                 #nullable enable
                                 
                                 namespace Astro.Generated;

                                 {GeneratedCodeAttribute}
                                 public sealed class {idClassName}ToStringConverter() : global::Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<{idClassName}, string>(
                                     v => v.ToString(),
                                     v => {idClassName}.Parse(v));
                                 """;
        context.AddSource($"Astro.Generated.{qualifiedName}EfCoreIdConverter.g.cs", efCoreConverterSource);

        var idConverterEfCoreExtSource = $$"""
                                           // <auto-generated/>

                                           #nullable enable

                                           namespace Astro.Generated;

                                           {{GeneratedCodeAttribute}}
                                           public static class {{idClassName}}ConventionExt
                                           {
                                               public static void Configure{{idClassName}}Conventions(this global::Microsoft.EntityFrameworkCore.ModelConfigurationBuilder configurationBuilder)
                                               {
                                                   configurationBuilder
                                                       .Properties<{{idClassName}}>()
                                                       .HaveConversion<{{idClassName}}ToStringConverter>();
                                               }
                                           }
                                           """;
        context.AddSource($"Astro.Generated.{qualifiedName}IdConventionExt.g.cs", idConverterEfCoreExtSource);
    }
}