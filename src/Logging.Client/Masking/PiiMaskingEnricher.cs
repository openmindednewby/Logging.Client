using Serilog.Core;
using Serilog.Events;

namespace Logging.Client.Masking;

/// <summary>Masks emails, phone numbers and sensitive-named values in every log event property.</summary>
public sealed class PiiMaskingEnricher : ILogEventEnricher
{
    private const string Redacted = "***REDACTED***";

    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        List<LogEventProperty>? changes = null;
        foreach (var (name, value) in logEvent.Properties)
        {
            var masked = Mask(name, value);
            if (!ReferenceEquals(masked, value))
                (changes ??= []).Add(new LogEventProperty(name, masked));
        }

        if (changes is null) return;

        foreach (var property in changes)
            logEvent.AddOrUpdateProperty(property);
    }

    internal static LogEventPropertyValue Mask(string? name, LogEventPropertyValue value)
    {
        if (name is not null && SensitivePropertyNames.Names.Contains(name))
            return new ScalarValue(Redacted);

        if (name is not null && PiiMaskingPolicy.IsPhoneName(name))
            return new ScalarValue(PiiMaskingPolicy.MaskPhoneNamedValue((value as ScalarValue)?.Value as string));

        return value switch
        {
            ScalarValue { Value: string s } => MaskScalar(value, s),
            StructureValue structure => MaskStructure(structure),
            SequenceValue sequence => MaskSequence(sequence),
            DictionaryValue dictionary => MaskDictionary(dictionary),
            _ => value,
        };
    }

    private static LogEventPropertyValue MaskScalar(LogEventPropertyValue original, string s)
    {
        var masked = PiiMaskingPolicy.MaskIfPii(s);
        return ReferenceEquals(masked, s) ? original : new ScalarValue(masked);
    }

    private static LogEventPropertyValue MaskStructure(StructureValue structure)
    {
        var copy = CopyOnFirstChange(structure.Properties, p =>
        {
            var masked = Mask(p.Name, p.Value);
            return ReferenceEquals(masked, p.Value) ? p : new LogEventProperty(p.Name, masked);
        });

        return copy is null ? structure : new StructureValue(copy, structure.TypeTag);
    }

    private static LogEventPropertyValue MaskSequence(SequenceValue sequence)
    {
        var copy = CopyOnFirstChange(sequence.Elements, e => Mask(null, e));
        return copy is null ? sequence : new SequenceValue(copy);
    }

    private static LogEventPropertyValue MaskDictionary(DictionaryValue dictionary)
    {
        List<KeyValuePair<ScalarValue, LogEventPropertyValue>>? copy = null;
        var index = 0;
        foreach (var (key, value) in dictionary.Elements)
        {
            var masked = Mask(key.Value as string, value);
            var changed = !ReferenceEquals(masked, value);
            if (copy is null && changed)
                copy = [.. dictionary.Elements.Take(index)];
            copy?.Add(new KeyValuePair<ScalarValue, LogEventPropertyValue>(key, masked));
            index++;
        }

        return copy is null ? dictionary : new DictionaryValue(copy);
    }

    private static List<T>? CopyOnFirstChange<T>(IReadOnlyList<T> items, Func<T, T> map)
        where T : class
    {
        List<T>? copy = null;
        for (var i = 0; i < items.Count; i++)
        {
            var mapped = map(items[i]);
            if (copy is null && ReferenceEquals(mapped, items[i])) continue;

            copy ??= new List<T>(items.Take(i));
            copy.Add(mapped);
        }

        return copy;
    }
}
