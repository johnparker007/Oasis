# MFME FML decoder integration

`Upstream/` is the single compiled C# snapshot from
[paul-maidment/mfme-fml-decoder](https://github.com/paul-maidment/mfme-fml-decoder).
`Upstream/upstream.json` pins the exact commit and SHA-256 hashes of the original
and integrated files. The upstream AGPL-3.0-or-later license is retained there.
No TypeScript, CLI entry point, upstream test code or build-time download is used.
The license and provenance manifest are copied into `ThirdParty/mfme-fml-decoder`
in build and publish output.

Oasis's adapter lives in `Features/FmlImport/FmlDecoderService.cs`; mapping,
asset copying, diagnostics and undo remain Oasis responsibilities. The obsolete
intermediate copy under `OasisEditor/Upstream` has been removed.

Two explicit source hooks retain Oasis's existing input-field presence tracking:
BaseComponent is partial, and ComponentParserBase calls `OasisSourcePresence`
when applying a parsed tag section. Their implementation is outside the vendor
tree. This distinguishes a missing input button number from an explicit zero,
including nested and legacy sections. The sync script checks both hook locations
and stops if upstream changes them. Other decoder files are copied unchanged.

## Updating

Fetch upstream into a separate, clean checkout, review the desired C# revision,
then run from the repository root (use the full commit SHA):

```powershell
python WindowsNetProjects/OasisEditor/scripts/sync_fml_decoder.py --checkout <checkout> --revision <sha>
python WindowsNetProjects/OasisEditor/scripts/sync_fml_decoder.py --check
dotnet test WindowsNetProjects/OasisEditor/Oasis.Authoring.RegressionTests --filter FullyQualifiedName~Fml
dotnet test WindowsNetProjects/OasisEditor/OasisEditor.Tests --filter FullyQualifiedName~Fml
dotnet build WindowsNetProjects/OasisEditor/OasisEditor.Mcp
python WindowsNetProjects/OasisEditor/OasisEditor.Mcp/verify_fml_import.py --upstream <checkout> --output <outside-repository-output>
```

Review API/profile/default changes against the adapter and mapper. Add regression
fixtures for new behaviour and run actual Editor/MCP import checks on disposable
projects. Upstream development is ongoing; pin reviewed updates rather than
tracking a moving branch at build time. Version support follows the upstream
profile catalogue and unsupported combinations remain explicit errors.
