using System.IO;
using Unity.VectorGraphics;
using UnityEngine;

public class SvgInScene : MonoBehaviour
{
	public TextAsset svgAsset;
	public VectorUtils.TessellationOptions tesselationOptions;

	public void Start()
	{
		tesselationOptions = new VectorUtils.TessellationOptions();
		tesselationOptions.StepDistance = 1f;

		initSVG();
	}

	private void initSVG()
	{
		// Dynamically import the SVG data, and tessellate the resulting vector scene.
		var sceneInfo = loadSVG();
        var geoms = VectorUtils.TessellateScene(sceneInfo.Scene, tesselationOptions);

        // Build a sprite with the tessellated geometry.
        var sprite = VectorUtils.BuildSprite(geoms, 10.0f, VectorUtils.Alignment.Center, Vector2.zero, 128, true);
        sprite.name = svgAsset.name;
        var spriteRenderer = gameObject.AddComponent<SpriteRenderer>(); // or get existing one
        spriteRenderer.sprite = sprite;
    }

	private SVGParser.SceneInfo loadSVG()
	{
		using (var reader = new StringReader(svgAsset.text))
		{ // not strictly needed but in case switch later.
			return SVGParser.ImportSVG(reader);
		}
	}
}