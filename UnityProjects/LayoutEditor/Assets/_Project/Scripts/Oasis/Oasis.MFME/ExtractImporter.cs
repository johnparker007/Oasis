//using MFMEExtract;
using Oasis.Layout;
using Oasis.MfmeTools.Shared.ExtractComponents;
using Oasis.MfmeTools.Shared.Extract;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using Oasis.MfmeTools.Shared.UnityWrappers;
using Oasis.LayoutEditor;

namespace Oasis.MFME
{
    public class ExtractImporter
    {
        public UnityEvent OnImportComplete = new UnityEvent();

        private LayoutObject _layoutObject = null;
        private View _baseView = null;

        public ExtractImporter()
        {
            Extractor.OnLayoutLoaded.AddListener(OnMFMEExtractLayoutLoaded);
        }

        private void OnMFMEExtractLayoutLoaded(MfmeTools.Shared.Extract.Layout layout)
        {
            ImportGamData(layout);
            ImportMameRomIdent(layout);

            _layoutObject = new LayoutObject();
            Editor.Instance.Project.Layout = _layoutObject;

            _baseView = Editor.Instance.Project.Layout.AddView(ViewController.kBaseViewName);

            foreach (ExtractComponentBase extractComponent in layout.Components)
            {
                if (extractComponent.GetType() == typeof(ExtractComponentLamp))
                {
                    ImportLamp((ExtractComponentLamp)extractComponent);
                }
                else if (extractComponent.GetType() == typeof(ExtractComponentCheckbox))
                {
                    ImportCheckbox((ExtractComponentCheckbox)extractComponent);
                }
                else if (extractComponent.GetType() == typeof(ExtractComponentButton))
                {
                    ImportButton((ExtractComponentButton)extractComponent);
                }
                else if (extractComponent.GetType() == typeof(ExtractComponentReel))
                {
                    ImportReel((ExtractComponentReel)extractComponent);
                }
                else if (extractComponent.GetType() == typeof(ExtractComponentBackground))
                {
                    ImportBackground((ExtractComponentBackground)extractComponent);
                }
                else if (extractComponent.GetType() == typeof(ExtractComponentSevenSegment))
                {
                    ImportSevenSegment((ExtractComponentSevenSegment)extractComponent);
                }
                else if (extractComponent.GetType() == typeof(ExtractComponentAlpha))
                {
                    ImportAlpha((ExtractComponentAlpha)extractComponent);
                }
                else if (extractComponent.GetType() == typeof(ExtractComponentAlphaNew))
                {
                    ImportAlphaNew((ExtractComponentAlphaNew)extractComponent);
                }
                else if (extractComponent.GetType() == typeof(ExtractComponentMatrixAlpha))
                {
                    ImportMatrixAlpha((ExtractComponentMatrixAlpha)extractComponent);
                }
                else
                {
                    Debug.LogWarning("Not imported!  " + extractComponent.GetType());
                }
            }

            OnImportComplete?.Invoke();
        }

        private void ImportGamData(MfmeTools.Shared.Extract.Layout layout)
        {
            Editor.Instance.Project.Settings.FruitMachine.Platform = 
                MAME.MameController.GetPlatformFromMfmeSystem(layout.GamFile.KeyValuePairs["System"][0]);
        }

        private void ImportMameRomIdent(MfmeTools.Shared.Extract.Layout layout)
        {
            Editor.Instance.Project.Settings.Mame.RomName = layout.MameRomIdent;
        }

        private void ImportLamp(ExtractComponentLamp extractComponentLamp)
        {
            ComponentLamp componentLamp = new ComponentLamp();

            componentLamp.Position = new UnityEngine.Vector2Int(
                extractComponentLamp.Position.X,
                extractComponentLamp.Position.Y);

            componentLamp.Size = new UnityEngine.Vector2Int(
                extractComponentLamp.Size.X,
                extractComponentLamp.Size.Y);

            // TODO - may want to make single componentLamp for each of the 12 possible mfme lamp elements in an mfme
            // lamp component:
            string bmpImageFilePath = null;
            string bmpImageFilename = extractComponentLamp.LampElements[0].BmpImageFilename;
            if (bmpImageFilename != null)
            {
                bmpImageFilePath = Path.Combine(Extractor.LayoutDirectoryPath,
                FileSystem.kLampsDirectoryName, extractComponentLamp.LampElements[0].BmpImageFilename);
            }

            //string bmpMaskImageFilePath = Path.Combine(
            //    Extractor.LayoutDirectoryPath, extractComponentLamp.LampElements[0].BmpMaskImageFilename);

            // TODO also need to figure out coin/note + effect inputs
            if (extractComponentLamp.HasButtonInput)
            {
                componentLamp.Input.Enabled = true;

                int mfmeButtonNumber = int.Parse(extractComponentLamp.ButtonNumberAsString);
                componentLamp.Input.ButtonNumber = mfmeButtonNumber;

                componentLamp.Input.Inverted = extractComponentLamp.Inverted;

                // TODO TEMP!  Just hardcode call for Impact, needs to check MFME layout platform:
                //componentLamp.Input.PortTag = 
                //    MameInputPortHelper.GetMamePortTagImpact(mfmeButtonNumber);

                //componentLamp.Input.FieldMask =
                //    MameInputPortHelper.GetMAMEPortInputMaskName(mfmeButtonNumber);

                // TODO - support for Shortcut 2... maybe also combined inputs like Shift+3 etc?
                componentLamp.Input.KeyCode = ShortcutKeyHelper.GetKeyCode(extractComponentLamp.Shortcut1);
            }

            //componentLamp.OasisImage = new Graphics.OasisImage(bmpImageFilePath, bmpMaskImageFilePath, true);
            if (bmpImageFilePath != null)
            {
                componentLamp.OasisImage = new Graphics.OasisImage(bmpImageFilePath, null, true);
            }

            // TODO - may want to make single componentLamp for each of the 12 possible mfme lamp elements in an mfme
            // lamp component:
            // all a bit of a hack for now, just use 1st of the 12 lamp elements
            ExtractComponentLamp.LampElement lampElement = extractComponentLamp.LampElements[0];

            componentLamp.Number = lampElement.Number; // TODO will need to be checking lamp HasValue since it's nullable

            componentLamp.OnColor = new UnityEngine.Color(
                lampElement.OnColor.ToColor().r,
                lampElement.OnColor.ToColor().g,
                lampElement.OnColor.ToColor().b);

            componentLamp.OffColor = new UnityEngine.Color(
                extractComponentLamp.OffImageColor.ToColor().r,
                extractComponentLamp.OffImageColor.ToColor().g,
                extractComponentLamp.OffImageColor.ToColor().b);

            componentLamp.TextColor = new UnityEngine.Color(
                extractComponentLamp.TextColor.ToColor().r,
                extractComponentLamp.TextColor.ToColor().g,
                extractComponentLamp.TextColor.ToColor().b);

            componentLamp.Name = "Lamp";

            // TODO these base text fields prob want doing in a single method for importing all components
            componentLamp.Text = extractComponentLamp.TextBoxText;
            componentLamp.FontName = extractComponentLamp.TextBoxFontName;
            componentLamp.FontStyle = extractComponentLamp.TextBoxFontStyle;
            int.TryParse(extractComponentLamp.TextBoxFontSize, out int fontSize);
            componentLamp.FontSize = fontSize;

            componentLamp.Outline = !extractComponentLamp.NoOutline;

            _baseView.AddComponent(componentLamp);
        }

        private void ImportCheckbox(ExtractComponentCheckbox extractComponentCheckbox)
        {
            ComponentSwitch componentSwitch = new ComponentSwitch();

            componentSwitch.Input.Enabled = true;
            componentSwitch.Input.ButtonNumber = extractComponentCheckbox.Number;
            // TODO import State

            _baseView.AddComponent(componentSwitch);
        }

        private void ImportButton(ExtractComponentButton extractComponentButton)
        {
            // new approach, in the LayoutEditor all buttons are simply lamps with input enabled:
            ExtractComponentLamp extractComponentLamp = new ExtractComponentLamp(extractComponentButton);

            ImportLamp(extractComponentLamp);
        }

        private void ImportReel(ExtractComponentReel extractComponentReel)
        {
            ComponentReel componentReel = new ComponentReel();

            componentReel.Position = new UnityEngine.Vector2Int(
                extractComponentReel.Position.X,
                extractComponentReel.Position.Y);

            componentReel.Size = new UnityEngine.Vector2Int(
                extractComponentReel.Size.X,
                extractComponentReel.Size.Y);

            string bandBmpImageFilePath = Path.Combine(Extractor.LayoutDirectoryPath,
                FileSystem.kReelsDirectoryName, extractComponentReel.BandBmpImageFilename);
            componentReel.BandOasisImage = new Graphics.OasisImage(bandBmpImageFilePath, null, true);


            // ******* START temp code for applying overlays to background image

            // TOIMPROVE - the 'overlay' will be changed so that the MfmeTools Extractor will have new settings:
            // - scrape Alpha overlays
            // - scrape Segemnt overlays
            // - potentially more component types
            // so for specific layouts, e.g: Rat Race by TommyC, we would scrape the Alpha overlay and that would
            // give us the Rats nose showing above the top edge of the Alpha.
            // ... so this code will be at at higher level, and will go through all component types to copy that overlay
            // onto the Background with transparency, not just Reels.

            ComponentBackground componentBackground = 
                (ComponentBackground)_baseView.Data.Components.Find(x => x.GetType() == typeof(ComponentBackground));

            if(componentBackground.OasisImage != null)
            {
                Graphics.OasisImage temporaryBackgroundOasisImage = componentBackground.OasisImage.Clone();
                Graphics.OasisImage temporaryOverlayOasisImage;
                if (extractComponentReel.HasOverlay)
                {
                    string overlayBmpImageFilePath = Path.Combine(Extractor.LayoutDirectoryPath,
                        FileSystem.kReelsDirectoryName, extractComponentReel.OverlayBmpImageFilename);

                    temporaryOverlayOasisImage = new Graphics.OasisImage(overlayBmpImageFilePath, null, true);
                }
                else
                {
                    temporaryOverlayOasisImage = new Graphics.OasisImage(componentReel.Size);
                }

                temporaryBackgroundOasisImage.Draw(temporaryOverlayOasisImage,
                    componentReel.Position.x,
                    componentBackground.Size.y - componentReel.Position.y - componentReel.Size.y);

                componentBackground.OasisImage.ImageData = temporaryBackgroundOasisImage.ImageData;

                // need to re-init the EditorComponentBackground so it's texture is rebuilt.
                // TOIMPROVE - should perhaps have something other that Initialise(...) in the
                // EditorComponent... perhaps a RebuildTextures or something similar?
                EditorComponent editorComponentBackground = _baseView.EditorView.GetEditorComponent(componentBackground);
                editorComponentBackground.Initialise(componentBackground);
            }


            // ******* END temp code for applying overlays to background image


            // we need a +1 for the reel but not the lamps, prob MFME <> MAME inconsistency
            componentReel.Number = extractComponentReel.Number + 1;
            componentReel.Stops = extractComponentReel.Stops;
            componentReel.Reversed = extractComponentReel.Reversed;

            // convert MFME's visible reel scaling to a simple float
            // MFME uses Reel Stops and Reel Height (not the standard x/y/width/height 'height')
            // this is crude since not worth coding MFME's fake reel perspective scaling, but should be reasonable enough

            // in MFME I think it's 50 of Height per visible symbol.  And then we need
            // Stops to know how many individual symbols on reel

            const int kMfmeHeightPerVisibleSymbol = 50;
            int stops = extractComponentReel.Stops;
            int height = extractComponentReel.Height;
            float visibleSymbols = (float)height / kMfmeHeightPerVisibleSymbol;
            float scale = visibleSymbols / stops;

            //            float halfHeight = height * 0.5f;
            //"HERE - BandOasisImage.Height should have very little effect on this calc from comparing Andy Capp and Nickelodeon"
            //            componentReel.VisibleScale2D = (halfHeight / componentReel.BandOasisImage.Height) * stops * 0.25f;

            componentReel.VisibleScale2D = scale;

            componentReel.Name = $"Reel {componentReel.Number}";

            componentReel.ReelSymbolText = new List<string>();
            for (int stopIndex = 0; stopIndex < stops; ++stopIndex)
            {
                componentReel.ReelSymbolText.Add($"Symbol {stopIndex}");
            }

            _baseView.AddComponent(componentReel);
        }

        private void ImportBackground(ExtractComponentBackground extractComponentBackground)
        {
            ComponentBackground componentBackground = new ComponentBackground();

            componentBackground.Position = new UnityEngine.Vector2Int(0, 0);
            componentBackground.Size = new UnityEngine.Vector2Int(
                extractComponentBackground.Size.X, extractComponentBackground.Size.Y);

            componentBackground.Color = new UnityEngine.Color(
                extractComponentBackground.Color.ToColor().r,
                extractComponentBackground.Color.ToColor().g,
                extractComponentBackground.Color.ToColor().b);

            if (extractComponentBackground.BmpImageFilename.Length > 0)
            {
                string bmpImageFilePath = Path.Combine(Extractor.LayoutDirectoryPath,
                    FileSystem.kBackgroundDirectoryName, extractComponentBackground.BmpImageFilename);

                componentBackground.OasisImage = new Graphics.OasisImage(bmpImageFilePath, null, false);

                // JP This is just test code - once the perspective quad selection tool and image/view 
                // creation stuff is working properly, this can be discarded:
                const bool kDebugTestOasisImageTransform = false;
                if(kDebugTestOasisImageTransform)
                {
                    if (componentBackground.OasisImage != null)
                    {
                        Graphics.OasisImage sourceImage = componentBackground.OasisImage;

                        // correct, but both x and y flipped:
                        //UnityEngine.Vector2Int pointA = new UnityEngine.Vector2Int(439, sourceImage.Height - 1312);
                        //UnityEngine.Vector2Int pointB = new UnityEngine.Vector2Int(1600, sourceImage.Height - 1312);
                        //UnityEngine.Vector2Int pointC = new UnityEngine.Vector2Int(1678, sourceImage.Height - 1811);
                        //UnityEngine.Vector2Int pointD = new UnityEngine.Vector2Int(360, sourceImage.Height - 1811);

                        // WORKING (for Full Thottle DX lower glass) - no x/y flip issue
                        UnityEngine.Vector2Int pointA = new UnityEngine.Vector2Int(360, sourceImage.Height - 1811);
                        UnityEngine.Vector2Int pointB = new UnityEngine.Vector2Int(1678, sourceImage.Height - 1811);
                        UnityEngine.Vector2Int pointC = new UnityEngine.Vector2Int(1600, sourceImage.Height - 1312);
                        UnityEngine.Vector2Int pointD = new UnityEngine.Vector2Int(439, sourceImage.Height - 1312);

                        //float targetAspectRatio = sourceImage.Width / (float)sourceImage.Height;
                        float targetAspectRatio = 1518 / (float)728;

                        Graphics.OasisImage transformedImage = Graphics.OasisImage.Transform(
                            sourceImage,
                            pointA,
                            pointB,
                            pointC,
                            pointD,
                            targetAspectRatio);

                        byte[] pngBytes = transformedImage.GetAsPngBytes();
                        string outputPath = Path.Combine(Application.persistentDataPath, "testTransform.png");

                        File.WriteAllBytes(outputPath, pngBytes);
                        Debug.LogError($"Saved test transform to {outputPath}");
                    }
                }
            }

            componentBackground.Name = "Background";

            _baseView.AddComponent(componentBackground);
        }

        private void ImportSevenSegment(ExtractComponentSevenSegment extractComponentSevenSegment)
        {
            Component7Segment component7Segment = new Component7Segment();

            component7Segment.Position = new UnityEngine.Vector2Int(
                extractComponentSevenSegment.Position.X,
                extractComponentSevenSegment.Position.Y);

            component7Segment.Size = new UnityEngine.Vector2Int(
                extractComponentSevenSegment.Size.X,
                extractComponentSevenSegment.Size.Y);

            component7Segment.Number = extractComponentSevenSegment.Number;

            component7Segment.Color = new UnityEngine.Color(
                extractComponentSevenSegment.SegmentOnColor.ToColor().r,
                extractComponentSevenSegment.SegmentOnColor.ToColor().g,
                extractComponentSevenSegment.SegmentOnColor.ToColor().b);

            component7Segment.Name = $"7 Segment {component7Segment.Number}";

            _baseView.AddComponent(component7Segment);
        }

        private void ImportAlpha(ExtractComponentAlpha extractComponentAlpha)
        {
            ComponentAlpha componentAlpha = new ComponentAlpha();

            componentAlpha.Position = new UnityEngine.Vector2Int(
                extractComponentAlpha.Position.X,
                extractComponentAlpha.Position.Y);

            componentAlpha.Size = new UnityEngine.Vector2Int(
                extractComponentAlpha.Size.X,
                extractComponentAlpha.Size.Y);

            componentAlpha.Reversed = extractComponentAlpha.Reversed;

            componentAlpha.Name = "Alpha";

            _baseView.AddComponent(componentAlpha);
        }

        private void ImportAlphaNew(ExtractComponentAlphaNew extractComponentAlphaNew)
        {
            ComponentAlpha componentAlpha = new ComponentAlpha();

            componentAlpha.Position = new UnityEngine.Vector2Int(
                extractComponentAlphaNew.Position.X,
                extractComponentAlphaNew.Position.Y);

            componentAlpha.Size = new UnityEngine.Vector2Int(
                extractComponentAlphaNew.Size.X,
                extractComponentAlphaNew.Size.Y);

            componentAlpha.Reversed = extractComponentAlphaNew.Reversed;

            componentAlpha.Name = "Alpha";

            _baseView.AddComponent(componentAlpha);
        }

        private void ImportMatrixAlpha(ExtractComponentMatrixAlpha extractComponentMatrixAlpha)
        {
            // TODO placeholder, treat dot matrix alpha as 16 segment alpha until written the Component/renderer
            // also, need to check do we get both matrix output and 16seg output from MAME drivers such as sc4?
            Debug.LogWarning("Importing Matrix Alpha as Alpha new for now");

// ******* TEMP CODE JUST TO GET A FUNCTIONAL ALPHA WORKING ***
ComponentAlpha componentAlpha = new ComponentAlpha();

componentAlpha.Position = new UnityEngine.Vector2Int(
    extractComponentMatrixAlpha.Position.X,
    extractComponentMatrixAlpha.Position.Y);

componentAlpha.Size = new UnityEngine.Vector2Int(
    extractComponentMatrixAlpha.Size.X,
    extractComponentMatrixAlpha.Size.Y);

componentAlpha.Reversed = false;

componentAlpha.Name = "Alpha";

_baseView.AddComponent(componentAlpha);

        }

    }
}
