using Serilog.Core;
using Serilog.Events;

namespace Logging.Client.Masking;

/// <summary>
/// Masks PII in every bound property of a log event: string scalars that look like an email
/// or phone number (see <see cref="PiiMaskingPolicy.LooksLikePhone"/>), the whole value of any
/// property named in <see cref="SensitivePropertyNames"/>, and any phone-named property. Walks
/// structures, sequences and dictionaries, so <c>{@Email}</c>, <c>{Email}</c> and a destructured
/// object's nested members are all covered.
/// </summary>
/// <remarks>
/// This is an enricher, not a destructuring policy, because Serilog converts strings with its
/// built-in scalar conversion before any <see cref="IDestructuringPolicy"/> is consulted, so
/// <see cref="PiiMaskingPolicy"/> is never offered a string. Enrichers run after binding and
/// before sinks, so the rendered message and every sink (Console, Loki, Sentry) see masked values.
/// Register it after all other enrichers. It runs on every event, so it allocates only when a
/// value actually changes.
/// </remarks>
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

    /// <summary>
    /// Returns a masked copy of <paramref name="value"/>, or the same instance when nothing
    /// in it needed masking.
    /// </summary>
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

    /// <summary>
    /// Maps <paramref name="items"/> and returns a new list only when some item changed
    /// (by reference); returns <c>null</c>, allocating nothing, when none did.
    /// </summary>
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
