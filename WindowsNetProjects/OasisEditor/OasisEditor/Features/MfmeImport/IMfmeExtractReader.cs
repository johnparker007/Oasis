namespace OasisEditor.Features.MfmeImport;

internal interface IMfmeExtractReader
{
    MfmeExtractReadResult Read(MfmeImportContext context);
}
