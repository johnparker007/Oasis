using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TempArcadeSimComponents
{
    public class ComponentBase : MonoBehaviour
    {
        public Vector2Int DataLayoutReadPosition;

        public string MFMEBmpImagePath;

        public static RectInt MFMESourceImageRectInt;

        public int MFMEZOrder;

        public Vector2Int MFMEPixelPosition;
        public Vector2Int MFMEPixelSize;

        //protected Renderer _renderer = null;

        //protected Machine _machine = null;
        //protected Color _scrapedPixel = Color.black;

        //protected EmulatorScraper EmulatorScraper
        //   {
        //	get
        //       {
        //		return _machine.EmulatorScraper;
        //       }
        //   }

        //public Machine Machine
        //   {
        //	get
        //       {
        //		return _machine;
        //       }
        //   }

        //protected virtual void Awake()
        //{
        //	_renderer = GetComponent<Renderer>();
        //	_machine = GetComponentInParent<Machine>();
        //}

        //protected virtual void Start () 
        //{
        //}

        //protected virtual void OnDestroy()
        //   {
        //   }

        //public virtual void UpdateComponent()
        //   {
        //	// this is a bit of a fudge while getting fast renderer working for engaged machine:
        //	if(_machine == null)
        //       {
        //		_machine = GetComponentInParent<Machine>();
        //	}

        //	if(_machine.RenderDataRecorder.RecorderMode != RenderDataRecorder.Mode.Play)
        //       {
        //		UpdateScrapedPixel();
        //	}
        //}

        //protected virtual void UpdateScrapedPixel()
        //   {
        //	if (EmulatorScraper.ScrapedTexture != null)
        //	{
        //		_scrapedPixel = EmulatorScraper.ScrapedTexture.GetPixel(DataLayoutReadPosition.x, DataLayoutReadPosition.y);
        //	}
        //}



    }

}
