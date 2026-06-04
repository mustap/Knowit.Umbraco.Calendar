using Knowit.Umbraco.Calendar.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;

namespace Knowit.Umbraco.Calendar.PropertyEditors;

/// <summary>
/// Converts the stored "Calendar Picker" JSON into a strongly-typed <see cref="CalendarRef"/> so
/// views can do <c>Model.Value&lt;CalendarRef&gt;("calendarProperty")</c>. Auto-discovered.
/// </summary>
public sealed class CalendarPickerValueConverter : PropertyValueConverterBase
{
    private readonly IJsonSerializer _jsonSerializer;

    public CalendarPickerValueConverter(IJsonSerializer jsonSerializer)
        => _jsonSerializer = jsonSerializer;

    public override bool IsConverter(IPublishedPropertyType propertyType)
        => propertyType.EditorAlias == CalendarPickerDataEditor.SchemaAlias;

    public override Type GetPropertyValueType(IPublishedPropertyType propertyType)
        => typeof(CalendarRef);

    public override PropertyCacheLevel GetPropertyCacheLevel(IPublishedPropertyType propertyType)
        => PropertyCacheLevel.Element;

    public override object? ConvertIntermediateToObject(
        IPublishedElement owner,
        IPublishedPropertyType propertyType,
        PropertyCacheLevel referenceCacheLevel,
        object? inter,
        bool preview)
    {
        if (inter is null)
        {
            return null;
        }

        // Depending on how the JSON editor round-trips, the intermediate value may already be a
        // string of JSON or a deserialized object — normalize to a JSON string either way.
        var json = inter as string ?? _jsonSerializer.Serialize(inter);
        if (string.IsNullOrWhiteSpace(json) || json == "null")
        {
            return null;
        }

        try
        {
            var calendar = _jsonSerializer.Deserialize<CalendarRef>(json);
            return string.IsNullOrEmpty(calendar?.CalendarId) ? null : calendar;
        }
        catch
        {
            // Malformed stored value — render nothing rather than throwing during page rendering.
            return null;
        }
    }
}
