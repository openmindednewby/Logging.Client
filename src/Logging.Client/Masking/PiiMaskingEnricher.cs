using Serilog.Core;
using Serilog.Events;

namespace Logging.Client.Masking;

/// <summary>
/// Masks PII in every bound property of a log event: string scalars that look like an email
/// or phone number, and the whole value of any property whose name is in
/// <see cref="SensitivePropertyNames"/>. Walks structures, sequences and dictionaries, so
/// <c>{@Email}</c>, <c>{Email}</c> and a destructured object's nested members are all covered.
/// </summary>
/// <remarks>
/// This is an enricher, not a destructuring policy, because Serilog converts strings with its
/// built-in scalar conversion before any <see cref="IDestructuringPolicy"/> is consulted, so
/// <see cref="PiiMaskingPolicy"/> is never offered a string. Enrichers run after binding and
/// before sinks, so the rendered message and every sink (Console, Loki, Sentry) see masked values.
/// Register it after all other enrichers.
/// </remarks>
public sealed class PiiMaskingEnricher : ILogEventEnricher
{
    private const string Redacted = "***REDACTED***";

    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var (name, value) in logEvent.Properties.ToList())
        {
            var masked = Mask(name, value);
            if (!ReferenceEquals(masked, value))
                logEvent.AddOrUpdateProperty(new LogEventProperty(name, masked));
        }
    }

    /// <summary>
    /// Returns a masked copy of <paramref name="value"/>, or the same instance when nothing
    /// in it needed masking.
    /// </summary>
    internal static LogEventPropertyValue Mask(string? name, LogEventPropertyValue value)
    {
        if (name is not null && SensitivePropertyNames.Names.Contains(name))
            return new ScalarValue(Redacted);

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
        return masked == s ? original : new ScalarValue(masked);
    }

    private static LogEventPropertyValue MaskStructure(StructureValue structure)
    {
        var changed = false;
        var properties = structure.Properties.Select(p =>
        {
            var masked = Mask(p.Name, p.Value);
            changed |= !ReferenceEquals(masked, p.Value);
            return new LogEventProperty(p.Name, masked);
        }).ToList();

        return changed ? new StructureValue(properties, structure.TypeTag) : structure;
    }

    private static LogEventPropertyValue MaskSequence(SequenceValue sequence)
    {
        var changed = false;
        var elements = sequence.Elements.Select(e =>
        {
            var masked = Mask(null, e);
            changed |= !ReferenceEquals(masked, e);
            return masked;
        }).ToList();

        return changed ? new SequenceValue(elements) : sequence;
    }

    private static LogEventPropertyValue MaskDictionary(DictionaryValue dictionary)
    {
        var changed = false;
        var entries = dictionary.Elements.Select(kv =>
        {
            var key = kv.Key.Value as string;
            var masked = Mask(key, kv.Value);
            changed |= !ReferenceEquals(masked, kv.Value);
            return new KeyValuePair<ScalarValue, LogEventPropertyValue>(kv.Key, masked);
        }).ToList();

        return changed ? new DictionaryValue(entries) : dictionary;
    }
}
