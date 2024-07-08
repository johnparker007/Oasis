//using MFMEExtract;
using Oasis.Layout;
using Oasis.MfmeTools.Shared.ExtractComponents;
using Oasis.MfmeTools.Shared.Extract;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;

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
            if(_layoutObject != null)
            {
                GameObject.Destroy(_layoutObject.gameObject);
            }

            GameObject layoutGameObject = new GameObject("Layout");
            _layoutObject = layoutGameObject.AddComponent<LayoutObject>();
            _layoutObject.transform.parent = Editor.Instance.transform;
            Editor.Instance.Layout = _layoutObject;

            _mfmeView = Editor.Instance.Layout.AddView(LayoutObject.kMfmeViewName);

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
                else
                {
                    Debug.LogError("Not imported!  " + extractComponent.GetType());
                }
            }

            OnImportComplete?.Invoke();
        }

        private void ImportLamp(ExtractComponentLamp extractComponentLamp)
        {
            GameObject componentLampGameObject = new GameObject();
            ComponentLamp componentLamp = (ComponentLamp)componentLampGameObject.AddComponent(typeof(ComponentLamp));
            componentLampGameObject.transform.SetParent(_mfmeView.transform);

            componentLamp.Position = new Vector2Int(
                extractComponentLamp.Position.X,
                extractComponentLamp.Position.Y);

            componentLamp.Size = new Vector2Int(
                extractComponentLamp.Size.X,
                extractComponentLamp.Size.Y);

            // TODO - may want to make single componentLamp for each of the 12 possible mfme lamp elements in an mfme
            // lamp component:
            string bmpImageFilePath = null;
            string bmpImageFilename = extractComponentLamp.LampElements[0].BmpImageFilename;
            if(bmpImageFilename != null)
            {
                bmpImageFilePath = Path.Combine(Extractor.LayoutDirectoryPath,
                FileSystem.kLampsDirectoryName, extractComponentLamp.LampElements[0].BmpImageFilename);
            }

            //string bmpMaskImageFilePath = Path.Combine(
            //    Extractor.LayoutDirectoryPath, extractComponentLamp.LampElements[0].BmpMaskImageFilename);

            // TODO also need to figure out coin/note + effect inputs
            if(extractComponentLamp.HasButtonInput)
            {
                componentLamp.Input.Enabled = true;

                int mfmeButtonNumber = int.Parse(extractComponentLamp.ButtonNumberAsString);

                componentLamp.Input.ButtonNumber = mfmeButtonNumber;

                // TODO TEMP!  Just hardcode call for Impact, needs to check MFME layout platform:
                //componentLamp.Input.PortTag = 
                //    MameInputPortHelper.GetMamePortTagImpact(mfmeButtonNumber);

                //componentLamp.Input.FieldMask =
                //    MameInputPortHelper.GetMAMEPortInputMaskName(mfmeButtonNumber);

                // TODO - support for Shortcut 2... maybe also combined inputs like Shift+3 etc?
                componentLamp.Input.KeyCode = ShortcutKeyHelper.GetKeyCode(extractComponentLamp.Shortcut1);
            }

            //componentLamp.OasisImage = new Graphics.OasisImage(bmpImageFilePath, bmpMaskImageFilePath, true);
            if(bmpImageFilePath != null)
            {
                componentLamp.OasisImage = new Graphics.OasisImage(bmpImageFilePath, null, true);
            }

            // TODO - may want to make single componentLamp for each of the 12 possible mfme lamp elements in an mfme
            // lamp component:
            componentLamp.Number = (int)extractComponentLamp.GetLampNumber(0); // TODO will need to be checking lamp HasValue since it's nullable

            _mfmeView.AddComponent(componentLamp);
        }

        private void ImportCheckbox(ExtractComponentCheckbox extractComponentCheckbox)
        {
            GameObject componentSwitchGameObject = new GameObject();
            ComponentSwitch componentSwitch = (ComponentSwitch)componentSwitchGameObject.AddComponent(typeof(ComponentSwitch));
            componentSwitchGameObject.transform.SetParent(_mfmeView.transform);

            _mfmeView.AddComponent(componentSwitch);
        }

        private void ImportButton(ExtractComponentButton extractComponentButton)
        {
            GameObject componentButtonGameObject = new GameObject();
            ComponentButton componentButton = (ComponentButton)componentButtonGameObject.AddComponent(typeof(ComponentButton));
            componentButtonGameObject.transform.SetParent(_mfmeView.transform);

            _mfmeView.AddComponent(componentButton);
        }

        private void ImportReel(ExtractComponentReel extractComponentReel)
        {
            GameObject componentReelGameObject = new GameObject();
            ComponentReel componentReel = (ComponentReel)componentReelGameObject.AddComponent(typeof(ComponentReel));
            componentReelGameObject.transform.SetParent(_mfmeView.transform);

            componentReel.Position = new Vector2Int(
                extractComponentReel.Position.X,
                extractComponentReel.Position.Y);

            componentReel.Size = new Vector2Int(
                extractComponentReel.Size.X,
                extractComponentReel.Size.Y);

            string bandBmpImageFilePath = Path.Combine(Extractor.LayoutDirectoryPath, 
                FileSystem.kReelsDirectoryName, extractComponentReel.BandBmpImageFilename);
            componentReel.BandOasisImage = new Graphics.OasisImage(bandBmpImageFilePath, null, true);

            if(extractComponentReel.HasOverlay)
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

            _mfmeView.AddComponent(componentReel);
        }

        private void ImportBackground(ExtractComponentBackground extractComponentBackground)
        {
            GameObject componentBackgroundGameObject = new GameObject();
            ComponentBackground componentBackground = 
                (ComponentBackground)componentBackgroundGameObject.AddComponent(typeof(ComponentBackground));

            componentBackgroundGameObject.transform.SetParent(_mfmeView.transform);

            componentBackground.Position = new Vector2Int(0, 0);
            componentBackground.Size = new Vector2Int(
                extractComponentBackground.Size.X, extractComponentBackground.Size.Y);

            string bmpImageFilePath = Path.Combine(Extractor.LayoutDirectoryPath, 
                FileSystem.kBackgroundDirectoryName, extractComponentBackground.BmpImageFilename);

            componentBackground.OasisImage = new Graphics.OasisImage(bmpImageFilePath, null, false);

            _mfmeView.AddComponent(componentBackground);
        }

        private void ImportSevenSegment(ExtractComponentSevenSegment extractComponentSevenSegment)
        {
            GameObject component7SegmentGameObject = new GameObject();
            Component7Segment component7Segment =
                (Component7Segment)component7SegmentGameObject.AddComponent(typeof(Component7Segment));

            component7SegmentGameObject.transform.SetParent(_mfmeView.transform);

            component7Segment.Position = new Vector2Int(
                extractComponentSevenSegment.Position.X,
                extractComponentSevenSegment.Position.Y);

            component7Segment.Size = new Vector2Int(
                extractComponentSevenSegment.Size.X,
                extractComponentSevenSegment.Size.Y);

            component7Segment.Number = extractComponentSevenSegment.Number;

            _mfmeView.AddComponent(component7Segment);
        }

        private void ImportAlpha(ExtractComponentAlpha extractComponentAlpha)
        {
            GameObject componentAlphaGameObject = new GameObject();
            ComponentAlpha componentAlpha =
                (ComponentAlpha)componentAlphaGameObject.AddComponent(typeof(ComponentAlpha));

            componentAlphaGameObject.transform.SetParent(_mfmeView.transform);

            componentAlpha.Position = new Vector2Int(
                extractComponentAlpha.Position.X,
                extractComponentAlpha.Position.Y);

            componentAlpha.Size = new Vector2Int(
                extractComponentAlpha.Size.X,
                extractComponentAlpha.Size.Y);

            componentAlpha.Reversed = extractComponentAlpha.Reversed;

            _mfmeView.AddComponent(componentAlpha);
        }

        private void ImportAlphaNew(ExtractComponentAlphaNew extractComponentAlphaNew)
        {
            GameObject componentAlphaGameObject = new GameObject();
            ComponentAlpha componentAlpha =
                (ComponentAlpha)componentAlphaGameObject.AddComponent(typeof(ComponentAlpha));

            componentAlphaGameObject.transform.SetParent(_mfmeView.transform);

            componentAlpha.Position = new Vector2Int(
                extractComponentAlphaNew.Position.X,
                extractComponentAlphaNew.Position.Y);

            componentAlpha.Size = new Vector2Int(
                extractComponentAlphaNew.Size.X,
                extractComponentAlphaNew.Size.Y);

            componentAlpha.Reversed = extractComponentAlphaNew.Reversed;

            _mfmeView.AddComponent(componentAlpha);
        }
    }

}

