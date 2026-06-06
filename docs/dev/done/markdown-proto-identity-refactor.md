# Markdown Proto Identity Refactor

Status: implemented. The bilingual Markdown export modes are implemented, and
the export data model keeps proto identity until the final render step.

## Previous State

Designer Toolkit supports Markdown language modes through `MarkdownTableLanguage`:

- `English`
- `Local`
- `Both`
- `Hybrid`

`DTK.BlueprintExport` rendered those modes by computing blueprint stats for a
specific `MarkdownRenderLanguage` and calling `DisplayName(...)` while building
the stats object.

The weakness was that the collected stats were keyed by rendered strings:

```csharp
public SortedDictionary<string, string> MaintenanceValues;
public SortedDictionary<string, string> ConstructionValues;
public SortedDictionary<string, int> ComponentCounts;
```

This worked for the current modes because stats could be recomputed per
language, but product/entity identity was lost before rendering.

## Implemented Shape

Stats now keep proto identity in the model:

```csharp
public Dictionary<ProductProto, string> ConstructionValues;
public Dictionary<VirtualProductProto, string> MaintenanceValues;
public Dictionary<Proto, int> ComponentCounts;
```

Names are rendered at the Markdown edge:

```csharp
private static string DisplayName(Proto proto, MarkdownRenderLanguage language)
{
    return LocalizedText(proto.Strings.Name, language);
}
```

Rows and dynamic folder columns are sorted by rendered display name for the
selected language, with proto ID as a deterministic tie-breaker when names
collide.

## Why This Matters

String-keyed stats can merge unrelated products or entities if two localized
names are identical. Proto-keyed stats keep values distinct and allow sorting,
deduplication, bilingual rendering, or future export layouts to choose display
text without changing the collection pass.

The existing output text remains stable for normal `English`, `Local`, `Both`,
and `Hybrid` exports. If two distinct protos render to the same text, Markdown
may contain duplicate-looking rows or columns, but their values are no longer
merged internally.

When the current game language is English (`en-US`), `Both` renders as a single
English export instead of producing duplicate English and local tables.

Markdown number formatting is configurable separately from text language:

- `Auto` uses English (`en-US`) separators for English tables and the current
  game locale for local/hybrid tables.
- `English separators` forces English decimal and thousands separators
  everywhere.
- `Local separators` forces the current game locale's decimal and thousands
  separators everywhere.
