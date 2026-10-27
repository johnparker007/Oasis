namespace OasisEditor;

public enum FaceValidationSeverity { Info, Warning, Error }

public sealed record FaceValidationDiagnostic(FaceValidationSeverity Severity, string Code, string Message);
