using MFMEExtract;
using Oasis.Layout;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Oasis.MFME
{
    public class ExtractImporter
    {
        public UnityEvent OnImportComplete = new UnityEvent();

        private LayoutEditor _layoutEditor = null;
        private LayoutObject _layoutObject = null;

        public ExtractImporter(LayoutEditor layoutEditor)
        {
            _layoutEditor = layoutEditor;
            Extractor.OnLayoutLoaded.AddListener(OnMFMEExtractLayoutLoaded);
        }

        private void OnMFMEExtractLayoutLoaded(MFMEExtract.Layout layout)
        {
            GameObject layoutGameObject = new GameObject();
            layoutGameObject.name = "Layout";
            _layoutObject = layoutGameObject.AddComponent<LayoutObject>();
            _layoutEditor.Layout = _layoutObject;
            // background, 7seg, alphaxccd
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

            _layoutObject.AddComponent(componentReel);
        }

        private void ImportBackground(ExtractComponentBackground extractComponentBackground)
        {
            ComponentBackground componentBackground = new ComponentBackground();

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

