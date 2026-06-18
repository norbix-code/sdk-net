#nullable enable annotations
#nullable disable warnings

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Norbix.Sdk.Types;

namespace Norbix.Sdk.Types.Api;

///<summary>
///A term plus its nested descendants. Returned by the term-tree endpoint,
///which fetches a whole parent→children tree in one call. Carries every
///<see cref="TermDto"/> field; <see cref="Children"/> is null for a leaf.
///</summary>
[DataContract]
public partial class TermTreeDto
{
    [DataMember]
    public virtual string Id { get; set; }

    [DataMember]
    public virtual string? TaxonomyId { get; set; }

    [DataMember]
    public virtual string? TaxonomyName { get; set; }

    [DataMember]
    public virtual string? ParentId { get; set; }

    [DataMember]
    public virtual int? Order { get; set; }

    [DataMember]
    public virtual string? Name { get; set; }

    [DataMember]
    public virtual Dictionary<string, string>? Names { get; set; }

    [DataMember]
    public virtual string? Description { get; set; }

    [DataMember]
    public virtual Dictionary<string, string>? Descriptions { get; set; }

    [DataMember]
    public virtual List<TermMultiParentDto>? MultiParents { get; set; }

    [DataMember]
    public virtual Object? Meta { get; set; }

    ///<summary>Nested child terms (direct children only at each level). Null when the term is a leaf.</summary>
    [DataMember]
    public virtual List<TermTreeDto>? Children { get; set; }
}

///<summary>
///A taxonomy plus its nested child taxonomies — the single-parent
///structure tree (e.g. Countries → Cities). When the request asked for
///terms, each node's own term tree is attached via <see cref="Terms"/>.
///</summary>
[DataContract]
public partial class TaxonomyTreeDto
{
    [DataMember]
    public virtual string ViewId { get; set; }

    [DataMember]
    public virtual string TaxonomyName { get; set; }

    [DataMember]
    public virtual string TaxonomySlug { get; set; }

    ///<summary>Parent taxonomy view-id, or null for a root taxonomy.</summary>
    [DataMember]
    public virtual string? ParentId { get; set; }

    ///<summary>Nested child taxonomies (direct children only at each level). Null when this taxonomy has no children.</summary>
    [DataMember]
    public virtual List<TaxonomyTreeDto>? Children { get; set; }

    ///<summary>This taxonomy's own term tree, populated only when the request asked to include terms. Null otherwise.</summary>
    [DataMember]
    public virtual List<TermTreeDto>? Terms { get; set; }
}

///<summary>
///Database
///</summary>
[NorbixRoute("/{version}/database/taxonomies/{taxonomyName}/terms/tree", "GET")]
[DataContract]
public partial class FindTermTreeRequest
    : CodeMashRequestBase, INorbixRequest<FindTermTreeResponse>
{
    [DataMember]
    public virtual string TaxonomyName { get; set; }

    [DataMember]
    public virtual string DatabaseIntegrationId { get; set; }

    ///<summary>Optional root term id. When set, only this term and its descendants are returned.</summary>
    [DataMember]
    public virtual string? RootTermId { get; set; }

    ///<summary>Optional depth cap (number of child levels) for a sub-tree query. Ignored when RootTermId is null.</summary>
    [DataMember]
    public virtual int? Depth { get; set; }
}

public partial class FindTermTreeResponse
    : ResponseBase
{
    ///<summary>The taxonomy's term tree(s). Each root carries its nested Children; multi-parent names are resolved on every node.</summary>
    public virtual List<TermTreeDto>? Tree { get; set; }
}

///<summary>
///Database
///</summary>
[NorbixRoute("/{version}/database/taxonomies/tree", "GET")]
[DataContract]
public partial class FindTaxonomyTreeRequest
    : CodeMashRequestBase, INorbixRequest<FindTaxonomyTreeResponse>
{
    [DataMember]
    public virtual string DatabaseIntegrationId { get; set; }

    ///<summary>When true, each taxonomy node carries its own term tree.</summary>
    [DataMember]
    public virtual bool? IncludeTerms { get; set; }
}

public partial class FindTaxonomyTreeResponse
    : ResponseBase
{
    ///<summary>The taxonomy structure tree(s). Each root carries its nested child taxonomies; with IncludeTerms, each node also carries its own term tree.</summary>
    public virtual List<TaxonomyTreeDto>? Tree { get; set; }
}
