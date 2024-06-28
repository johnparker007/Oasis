using Oasis.MfmeTools.Shared.ExtractComponents;
using Oasis.MfmeTools.Shared.JsonDataStructures;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oasis.MfmeTools.Shared.Extract
{
    [Serializable]
    public class Layout
    {
        public string ASName;

        public Vector2IntJSON BackgroundImageSize;

        public List<ExtractComponentBase> Components = new List<ExtractComponentBase>();

        public ExtractComponentBackground Background
        {
            get
            {
                return (ExtractComponentBackground)Components.FirstOrDefault(x => x.GetType() == typeof(ExtractComponentBackground));
            }
        }

        public bool IsOutsideLayoutWindow(ExtractComponentBase extractComponentBase)
        {
            return extractComponentBase.Position.X > Background.Size.X
                || extractComponentBase.Position.Y > Background.Size.Y
                || (extractComponentBase.Position.X + extractComponentBase.Size.X) < 0
                || (extractComponentBase.Position.Y + extractComponentBase.Size.Y) < 0;
        }
    }
}
