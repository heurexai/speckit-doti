namespace HxScaffoldSample;

/// <summary>
/// Marks a serializable data contract (DTO). Architecture rules require these to live in the
/// library, never in the CLI — the "attribute access" family checks this.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class SerializableContractAttribute : Attribute;
