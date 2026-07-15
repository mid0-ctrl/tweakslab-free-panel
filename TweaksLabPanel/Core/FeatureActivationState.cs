namespace TweaksLabPanel.Core;

public enum FeatureActivationState
{
    Enabled,
    Disabled,
    Unknown,
    NotSupported,
}

public sealed record BiosFeaturesSnapshot(
    FeatureActivationState Tpm,
    FeatureActivationState SecureBoot,
    FeatureActivationState Xmp,
    string? XmpDetail);
