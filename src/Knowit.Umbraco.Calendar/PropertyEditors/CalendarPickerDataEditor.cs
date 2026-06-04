using Umbraco.Cms.Core.PropertyEditors;

namespace Knowit.Umbraco.Calendar.PropertyEditors;

/// <summary>
/// Server-side schema for the "Calendar Picker" property editor. Auto-discovered by Umbraco via
/// the <see cref="DataEditorAttribute"/>. The stored value is JSON describing the picked
/// <c>CalendarRef</c>; the Property Editor UI (registered in umbraco-package.json) edits it.
/// </summary>
[DataEditor(
    alias: SchemaAlias,
    ValueType = ValueTypes.Json)]
public sealed class CalendarPickerDataEditor : DataEditor
{
    /// <summary>Schema alias linking the C# schema to the backoffice UI (propertyEditorSchemaAlias).</summary>
    public const string SchemaAlias = "Knowit.Umbraco.Calendar.Picker";

    public CalendarPickerDataEditor(IDataValueEditorFactory dataValueEditorFactory)
        : base(dataValueEditorFactory)
    {
    }
}
