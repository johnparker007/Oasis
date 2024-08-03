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

namespace Oasis.MFME
{
    public class ExtractImporter
    {
        public UnityEvent OnImportComplete = new UnityEvent();

        private LayoutObject _layoutObject = null;
        private View _mfmeView = null;

        public ExtractImporter()
        {
            Extractor.OnLayoutLoaded.AddListener(OnMFMEExtractLayoutLoaded);
        }

        private void OnMFMEExtractLayoutLoaded(MfmeTools.Shared.Extract.Layout layout)
        {
            if (_layoutObject != null)
            {
                GameObject.Destroy(_layoutObject.gameObject);
            }

            GameObject layoutGameObject = new GameObject("Layout");
            _layoutObject = layoutGameObject.AddComponent<LayoutObject>();
            _layoutObject.transform.parent = Editor.Instance.transform;
            Editor.Instance.Project.Layout = _layoutObject;

            _mfmeView = Editor.Instance.Project.Layout.AddView(LayoutObject.kMfmeViewName);

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

        private void ImportLamp(ExtractComponentLamp extractComponentLamp)
        {
            GameObject componentLampGameObject = new GameObject();
            ComponentLamp componentLamp = (ComponentLamp)componentLampGameObject.AddComponent(typeof(ComponentLamp));
            componentLampGameObject.transform.SetParent(_mfmeView.transform);

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
            componentLamp.Text = extractComponentLamp.TextBoxText;

            componentLamp.Outline = !extractComponentLamp.NoOutline;

            _mfmeView.AddComponent(componentLamp);
        }

        private void ImportCheckbox(ExtractComponentCheckbox extractComponentCheckbox)
        {
            GameObject componentSwitchGameObject = new GameObject();
            ComponentSwitch componentSwitch = (ComponentSwitch)componentSwitchGameObject.AddComponent(typeof(ComponentSwitch));
            componentSwitchGameObject.transform.SetParent(_mfmeView.transform);

            componentSwitch.Input.Enabled = true;
            componentSwitch.Input.ButtonNumber = extractComponentCheckbox.Number;
            // TODO import State

            _mfmeView.AddComponent(componentSwitch);
        }

        private void ImportButton(ExtractComponentButton extractComponentButton)
        {
            // new approach, in the LayoutEditor all buttons are simply lamps with input enabled:
            ExtractComponentLamp extractComponentLamp = new ExtractComponentLamp(extractComponentButton);

            ImportLamp(extractComponentLamp);
        }

        private void ImportReel(ExtractComponentReel extractComponentReel)
        {
            GameObject componentReelGameObject = new GameObject();
            ComponentReel componentReel = (ComponentReel)componentReelGameObject.AddComponent(typeof(ComponentReel));
            componentReelGameObject.transform.SetParent(_mfmeView.transform);

            componentReel.Position = new UnityEngine.Vector2Int(
                extractComponentReel.Position.X,
                extractComponentReel.Position.Y);

            componentReel.Size = new UnityEngine.Vector2Int(
                extractComponentReel.Size.X,
                extractComponentReel.Size.Y);

            string bandBmpImageFilePath = Path.Combine(Extractor.LayoutDirectoryPath,
                FileSystem.kReelsDirectoryName, extractComponentReel.BandBmpImageFilename);
            componentReel.BandOasisImage = new Graphics.OasisImage(bandBmpImageFilePath, null, true);

            if (extractComponentReel.HasOverlay)
            {
                string overlayBmpImageFilePath = Path.Combine(Extractor.LayoutDirectoryPath,
                    FileSystem.kReelsDirectoryName, extractComponentReel.OverlayBmpImageFilename);
                componentReel.OverlayOasisImage = new Graphics.OasisImage(overlayBmpImageFilePath, null, true);
            }

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
            componentReel.Text = "";
            for (int stopIndex = 0; stopIndex < stops; ++stopIndex)
            {
                componentReel.Text += $"Symbol {stopIndex}";
                if (stopIndex < stops - 1)
                {
                    componentReel.Text += ", ";
                }
            }

            _mfmeView.AddComponent(componentReel);
        }

        private void ImportBackground(ExtractComponentBackground extractComponentBackground)
        {
            GameObject componentBackgroundGameObject = new GameObject();
            ComponentBackground componentBackground =
                (ComponentBackground)componentBackgroundGameObject.AddComponent(typeof(ComponentBackground));

            componentBackgroundGameObject.transform.SetParent(_mfmeView.transform);

            componentBackground.Position = new UnityEngine.Vector2Int(0, 0);
            componentBackground.Size = new UnityEngine.Vector2Int(
                extractComponentBackground.Size.X, extractComponentBackground.Size.Y);

            // XXX TODO TEMP HACK FOR TESTING, to be replaced once we are scraping the various
            // additional background info from the Mfme component:
            componentBackground.Color = new UnityEngine.Color(0.5f, 0f, 0f);

            if (extractComponentBackground.BmpImageFilename.Length > 0)
            {
                string bmpImageFilePath = Path.Combine(Extractor.LayoutDirectoryPath,
                    FileSystem.kBackgroundDirectoryName, extractComponentBackground.BmpImageFilename);

                componentBackground.OasisImage = new Graphics.OasisImage(bmpImageFilePath, null, false);
            }

            componentBackground.Name = "Background";

            _mfmeView.AddComponent(componentBackground);
        }

        private void ImportSevenSegment(ExtractComponentSevenSegment extractComponentSevenSegment)
        {
            GameObject component7SegmentGameObject = new GameObject();
            Component7Segment component7Segment =
                (Component7Segment)component7SegmentGameObject.AddComponent(typeof(Component7Segment));

            component7SegmentGameObject.transform.SetParent(_mfmeView.transform);

            component7Segment.Position = new UnityEngine.Vector2Int(
                extractComponentSevenSegment.Position.X,
                extractComponentSevenSegment.Position.Y);

            component7Segment.Size = new UnityEngine.Vector2Int(
                extractComponentSevenSegment.Size.X,
                extractComponentSevenSegment.Size.Y);

            component7Segment.Number = extractComponentSevenSegment.Number;

            component7Segment.Name = $"7 Segment {component7Segment.Number}";

            _mfmeView.AddComponent(component7Segment);
        }

        private void ImportAlpha(ExtractComponentAlpha extractComponentAlpha)
        {
            GameObject componentAlphaGameObject = new GameObject();
            ComponentAlpha componentAlpha =
                (ComponentAlpha)componentAlphaGameObject.AddComponent(typeof(ComponentAlpha));

            componentAlphaGameObject.transform.SetParent(_mfmeView.transform);

            componentAlpha.Position = new UnityEngine.Vector2Int(
                extractComponentAlpha.Position.X,
                extractComponentAlpha.Position.Y);

            componentAlpha.Size = new UnityEngine.Vector2Int(
                extractComponentAlpha.Size.X,
                extractComponentAlpha.Size.Y);

            componentAlpha.Reversed = extractComponentAlpha.Reversed;

            componentAlpha.Name = "Alpha";

            _mfmeView.AddComponent(componentAlpha);
        }

        private void ImportAlphaNew(ExtractComponentAlphaNew extractComponentAlphaNew)
        {
            GameObject componentAlphaGameObject = new GameObject();
            ComponentAlpha componentAlpha =
                (ComponentAlpha)componentAlphaGameObject.AddComponent(typeof(ComponentAlpha));

            componentAlphaGameObject.transform.SetParent(_mfmeView.transform);

            componentAlpha.Position = new UnityEngine.Vector2Int(
                extractComponentAlphaNew.Position.X,
                extractComponentAlphaNew.Position.Y);

            componentAlpha.Size = new UnityEngine.Vector2Int(
                extractComponentAlphaNew.Size.X,
                extractComponentAlphaNew.Size.Y);

            componentAlpha.Reversed = extractComponentAlphaNew.Reversed;

            componentAlpha.Name = "Alpha";

            _mfmeView.AddComponent(componentAlpha);
        }

        private void ImportMatrixAlpha(ExtractComponentMatrixAlpha extractComponentMatrixAlpha)
        {
            // TODO placeholder, treat dot matrix alpha as 16 segment alpha until written the Component/renderer
            // also, need to check do we get both matrix output and 16seg output from MAME drivers such as sc4?
            Debug.LogWarning("Importing Matrix Alpha as Alpha new for now");

// ******* TEMP CODE JUST TO GET A FUNCTIONAL ALPHA WORKING ***
GameObject componentAlphaGameObject = new GameObject();
ComponentAlpha componentAlpha =
    (ComponentAlpha)componentAlphaGameObject.AddComponent(typeof(ComponentAlpha));

componentAlphaGameObject.transform.SetParent(_mfmeView.transform);

componentAlpha.Position = new UnityEngine.Vector2Int(
    extractComponentMatrixAlpha.Position.X,
    extractComponentMatrixAlpha.Position.Y);

componentAlpha.Size = new UnityEngine.Vector2Int(
    extractComponentMatrixAlpha.Size.X,
    extractComponentMatrixAlpha.Size.Y);

componentAlpha.Reversed = false;

componentAlpha.Name = "Alpha";

_mfmeView.AddComponent(componentAlpha);

        }

    }
}
