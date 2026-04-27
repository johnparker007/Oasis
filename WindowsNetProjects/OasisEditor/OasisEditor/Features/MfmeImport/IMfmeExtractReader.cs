namespace OasisEditor.Features.MfmeImport;

internal interface IMfmeExtractReader
{
    MfmeImportResult Read(MfmeImportContext context);
}
