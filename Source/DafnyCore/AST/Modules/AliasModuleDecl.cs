using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.CodeAnalysis;
using SymbolKind = OmniSharp.Extensions.LanguageServer.Protocol.Models.SymbolKind;

namespace Microsoft.Dafny;

/// <summary>
/// Represents "module name = path;", where name is an identifier and path is a possibly qualified name.
/// </summary>
public class AliasModuleDecl : ModuleDecl, ICanFormat {
  public ModuleQualifiedId TargetQId; // generated by the parser, this is looked up
  public List<IOrigin> Exports; // list of exports sets
  [FilledInDuringResolution] public bool ShadowsLiteralModule;  // initialized early during Resolution (and used not long after that); true for "import opened A = A" where "A" is a literal module in the same scope

  public AliasModuleDecl(Cloner cloner, AliasModuleDecl original, ModuleDefinition enclosingModule)
    : base(cloner, original, enclosingModule) {
    Opened = original.Opened;
    if (original.TargetQId != null) { // TODO is this null check necessary?
      TargetQId = new ModuleQualifiedId(cloner, original.TargetQId);

      /*
       * Refinement cloning happens in PreResolver, which is after the ModuleQualifiedId.Root fields are set,
       * so this field must be copied as part of refinement cloning.
       * However, letting refinement cloning set CloneResolvedFields==true causes exceptions for an uninvestigated reason,
       * so we will clone this field even when !CloneResolvedFields.
       */
      TargetQId.Root = original.TargetQId.Root;
    }
    Exports = original.Exports;
  }

  public AliasModuleDecl(DafnyOptions options, SourceOrigin origin, ModuleQualifiedId path, Name name, Attributes attributes,
    ModuleDefinition enclosingModule, bool opened, List<IOrigin> exports, Guid cloneId)
    : base(options, origin, name, attributes, enclosingModule, cloneId) {
    Contract.Requires(path != null && path.Path.Count > 0);
    Contract.Requires(exports != null);
    Contract.Requires(exports.Count == 0 || path.Path.Count == 1);
    TargetQId = path;
    Opened = opened;
    Exports = exports;
  }

  public override bool Opened { get; }

  public override ModuleDefinition Dereference() { return Signature.ModuleDef; }

  public bool SetIndent(int indentBefore, TokenNewIndentCollector formatter) {
    if (OwnedTokens.FirstOrDefault() is { } theToken) {
      formatter.SetOpeningIndentedRegion(theToken, indentBefore);
    }

    return true;
  }

  /// <summary>
  /// If no explicit name is given for an import declaration,
  /// Then we consider this as a reference, not a declaration, from the IDE perspective.
  /// So any further references to the imported module then resolve directly to the module,
  /// Not to this import declaration.
  ///
  /// Code wise, it might be better not to let AliasModuleDecl inherit from Declaration,
  /// since it is not always a declaration. 
  /// </summary>
  public override TokenRange NavigationRange => HasAlias ? base.NavigationRange : (TargetQId.Decl?.NavigationRange ?? base.NavigationRange);

  public bool HasAlias => NameNode.Origin.IsSet();

  public override SymbolKind? Kind => !HasAlias ? null : base.Kind;

  public override IEnumerable<INode> Children => base.Children.Concat(new INode[] { TargetQId });
}