using MFMEExtract;
using Oasis.Layout;
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

        private Editor _layoutEditor = null;
        private LayoutObject _layoutObject = null;

        public ExtractImporter(Editor layoutEditor)
        {
            _layoutEditor = layoutEditor;
            Extractor.OnLayoutLoaded.AddListener(OnMFMEExtractLayoutLoaded);
        }

        private void OnMFMEExtractLayoutLoaded(MFMEExtract.Layout layout)
        {
            GameObject layoutGameObject = new GameObject("Layout");
            _layoutObject = layoutGameObject.AddComponent<LayoutObject>();
            _layoutObject.transform.parent = _layoutEditor.transform;
            _layoutObject.LayoutEditor = _layoutEditor;
            _layoutEditor.Layout = _layoutObject;
            

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
            componentLampGameObject.transform.SetParent(_layoutObject.transform);

            componentLamp.RectInt = new RectInt(
                extractComponentLamp.Position.X,
                extractComponentLamp.Position.Y,
                extractComponentLamp.Size.X,
                extractComponentLamp.Size.Y);

            // TODO - may want to make single componentLamp for each of the 12 possible mfme lamp elements in an mfme
            // lamp component:
            string bmpImageFilePath = Path.Combine(
                Extractor.LayoutDirectoryPath, extractComponentLamp.LampElements[0].BmpImageFilename);

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
            componentLamp.OasisImage = new Graphics.OasisImage(bmpImageFilePath, null, true);

// TODO - may want to make single componentLamp for each of the 12 possible mfme lamp elements in an mfme
// lamp component:
componentLamp.Number = (int)extractComponentLamp.GetLampNumber(0); // TODO will need to be checking lamp HasValue since it's nullable

            _layoutObject.AddComponent(componentLamp);
        }

        private void ImportCheckbox(ExtractComponentCheckbox extractComponentCheckbox)
        {
            GameObject componentSwitchGameObject = new GameObject();
            ComponentSwitch componentSwitch = (ComponentSwitch)componentSwitchGameObject.AddComponent(typeof(ComponentSwitch));
            componentSwitchGameObject.transform.SetParent(_layoutObject.transform);

            _layoutObject.AddComponent(componentSwitch);
        }

        private void ImportButton(ExtractComponentButton extractComponentButton)
        {
            GameObject componentButtonGameObject = new GameObject();
            ComponentButton componentButton = (ComponentButton)componentButtonGameObject.AddComponent(typeof(ComponentButton));
            componentButtonGameObject.transform.SetParent(_layoutObject.transform);

            _layoutObject.AddComponent(componentButton);
        }

        private void ImportReel(ExtractComponentReel extractComponentReel)
        {
            GameObject componentReelGameObject = new GameObject();
            ComponentReel componentReel = (ComponentReel)componentReelGameObject.AddComponent(typeof(ComponentReel));
            componentReelGameObject.transform.SetParent(_layoutObject.transform);

            componentReel.RectInt = new RectInt(
                extractComponentReel.Position.X,
                extractComponentReel.Position.Y,
                extractComponentReel.Size.X,
                extractComponentReel.Size.Y);

            string bandBmpImageFilePath = Path.Combine(
                Extractor.LayoutDirectoryPath, extractComponentReel.BandBmpImageFilename);

            componentReel.BandOasisImage = new Graphics.OasisImage(bandBmpImageFilePath, null, true);
            // we need a +1 for the reel but not the lamps, prob MFME <> MAME inconsistency
            componentReel.Number = extractComponentReel.Number + 1;
            componentReel.Reversed = extractComponentReel.Reversed;

            _layoutObject.AddComponent(componentReel);
        }

        private void ImportBackground(ExtractComponentBackground extractComponentBackground)
        {
            GameObject componentBackgroundGameObject = new GameObject();
            ComponentBackground componentBackground = 
                (ComponentBackground)componentBackgroundGameObject.AddComponent(typeof(ComponentBackground));

            componentBackgroundGameObject.transform.SetParent(_layoutObject.transform);

            componentBackground.RectInt = new RectInt(
                0, 0, extractComponentBackground.Size.X, extractComponentBackground.Size.Y);

            string bmpImageFilePath = Path.Combine(
                Extractor.LayoutDirectoryPath, extractComponentBackground.BmpImageFilename);

            componentBackground.OasisImage = new Graphics.OasisImage(bmpImageFilePath, null, false);

            _layoutObject.AddComponent(componentBackground);
        }

        private void ImportSevenSegment(ExtractComponentSevenSegment extractComponentSevenSegment)
        {
            GameObject component7SegmentGameObject = new GameObject();
            Component7Segment component7Segment =
                (Component7Segment)component7SegmentGameObject.AddComponent(typeof(Component7Segment));

            component7SegmentGameObject.transform.SetParent(_layoutObject.transform);

            component7Segment.RectInt = new RectInt(
                extractComponentSevenSegment.Position.X,
                extractComponentSevenSegment.Position.Y,
                extractComponentSevenSegment.Size.X,
                extractComponentSevenSegment.Size.Y);

            component7Segment.Number = extractComponentSevenSegment.Number;

            _layoutObject.AddComponent(component7Segment);
        }

        private void ImportAlpha(ExtractComponentAlpha extractComponentAlpha)
        {
            GameObject componentAlphaGameObject = new GameObject();
            ComponentAlpha componentAlpha =
                (ComponentAlpha)componentAlphaGameObject.AddComponent(typeof(ComponentAlpha));

            componentAlphaGameObject.transform.SetParent(_layoutObject.transform);

            componentAlpha.RectInt = new RectInt(
                extractComponentAlpha.Position.X,
                extractComponentAlpha.Position.Y,
                extractComponentAlpha.Size.X,
                extractComponentAlpha.Size.Y);

            componentAlpha.Reversed = extractComponentAlpha.Reversed;

            _layoutObject.AddComponent(componentAlpha);
        }

        private void ImportAlphaNew(ExtractComponentAlphaNew extractComponentAlphaNew)
        {
            GameObject componentAlphaGameObject = new GameObject();
            ComponentAlpha componentAlpha =
                (ComponentAlpha)componentAlphaGameObject.AddComponent(typeof(ComponentAlpha));

            componentAlphaGameObject.transform.SetParent(_layoutObject.transform);

            componentAlpha.RectInt = new RectInt(
                extractComponentAlphaNew.Position.X,
                extractComponentAlphaNew.Position.Y,
                extractComponentAlphaNew.Size.X,
                extractComponentAlphaNew.Size.Y);

            componentAlpha.Reversed = extractComponentAlphaNew.Reversed;

            _layoutObject.AddComponent(componentAlpha);
        }
    }

}

