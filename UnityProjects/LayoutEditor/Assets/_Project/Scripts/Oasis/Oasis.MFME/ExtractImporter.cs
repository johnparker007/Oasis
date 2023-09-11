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
            ComponentLamp componentLamp = new ComponentLamp();

            componentLamp.RectInt = new RectInt(
                extractComponentLamp.Position.X,
                extractComponentLamp.Position.Y,
                extractComponentLamp.Size.X,
                extractComponentLamp.Size.Y);

            string bmpImageFilePath = Path.Combine(
                Extractor.LayoutDirectoryPath, extractComponentLamp.LampElements[0].BmpImageFilename);

            //string bmpMaskImageFilePath = Path.Combine(
            //    Extractor.LayoutDirectoryPath, extractComponentLamp.LampElements[0].BmpMaskImageFilename);

            //componentLamp.OasisImage = new Graphics.OasisImage(bmpImageFilePath, bmpMaskImageFilePath, true);
            componentLamp.OasisImage = new Graphics.OasisImage(bmpImageFilePath, null, true);

// TODO - may want to make single componentLamp for each of the 12 possible mfme lamp elements in an mfme
// lamp component:
componentLamp.Number = (int)extractComponentLamp.GetLampNumber(0); // TODO will need to be checking lamp HasValue since it's nullable

            _layoutObject.AddComponent(componentLamp);
        }

        private void ImportCheckbox(ExtractComponentCheckbox extractComponentCheckbox)
        {
            ComponentSwitch componentSwitch = new ComponentSwitch();

            _layoutObject.AddComponent(componentSwitch);
        }

        private void ImportButton(ExtractComponentButton extractComponentButton)
        {
            ComponentButton componentButton = new ComponentButton();

            _layoutObject.AddComponent(componentButton);
        }

        private void ImportReel(ExtractComponentReel extractComponentReel)
        {
            ComponentReel componentReel = new ComponentReel();

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

            _layoutObject.AddComponent(componentReel);
        }

        private void ImportBackground(ExtractComponentBackground extractComponentBackground)
        {
            ComponentBackground componentBackground = new ComponentBackground();

            componentBackground.RectInt = new RectInt(
                0, 0, extractComponentBackground.Size.X, extractComponentBackground.Size.Y);

            string bmpImageFilePath = Path.Combine(
                Extractor.LayoutDirectoryPath, extractComponentBackground.BmpImageFilename);

            componentBackground.OasisImage = new Graphics.OasisImage(bmpImageFilePath, null, false);

            _layoutObject.AddComponent(componentBackground);
        }

        private void ImportSevenSegment(ExtractComponentSevenSegment extractComponentSevenSegment)
        {
            ComponentSevenSegment componentSevenSegment = new ComponentSevenSegment();

            _layoutObject.AddComponent(componentSevenSegment);
        }

        private void ImportAlpha(ExtractComponentAlpha extractComponentAlpha)
        {
            ComponentAlpha componentAlpha = new ComponentAlpha();

            _layoutObject.AddComponent(componentAlpha);
        }

        private void ImportAlphaNew(ExtractComponentAlphaNew extractComponentAlphaNew)
        {
            ComponentAlpha componentAlpha = new ComponentAlpha();

            _layoutObject.AddComponent(componentAlpha);
        }
    }

}

