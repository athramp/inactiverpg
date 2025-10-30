// Assets/Editor/CreateWizardAttackAnim.cs
using UnityEngine;
using UnityEditor;
using System.Linq;

public static class CreateWizardAttackAnim
{
    [MenuItem("Tools/Animations/Create Wizard Attack (8x1, 12 FPS)")]
    public static void CreateFromSelected()
    {
        var tex = Selection.activeObject as Texture2D;
        if (!tex)
        {
            EditorUtility.DisplayDialog("No texture selected",
                "Select your 8x1 wizard attack spritesheet (Texture2D PNG) in the Project view.",
                "OK");
            return;
        }

        string path = AssetDatabase.GetAssetPath(tex);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null)
        {
            EditorUtility.DisplayDialog("Importer error", "Could not get TextureImporter.", "OK");
            return;
        }

        // --- Slice: 8 frames horizontally, pivot at BottomCenter ---
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.alphaIsTransparency = true;

        // Ensure point filtering if you want crisp pixel art (optional)
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;

        // Compute rects
        int columns = 8;
        int width = tex.width / columns;
        int height = tex.height;

        var metas = new SpriteMetaData[columns];
        for (int i = 0; i < columns; i++)
        {
            metas[i] = new SpriteMetaData
            {
                name = $"{tex.name}_{i:D2}",
                rect = new Rect(i * width, 0, width, height),
                alignment = (int)SpriteAlignment.Center, // pivot centered on wizard
                pivot = new Vector2(0.5f, 0.5f)
            };
        }

        importer.spritesheet = metas;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        // --- Load sliced sprites in correct order ---
        var assets = AssetDatabase.LoadAllAssetsAtPath(path);
        var sprites = assets.OfType<Sprite>()
                            .OrderBy(s => s.name)
                            .ToArray();

        if (sprites.Length != columns)
        {
            EditorUtility.DisplayDialog(
                "Slicing mismatch",
                $"Expected {columns} sprites after slicing, found {sprites.Length}. " +
                "Check the texture width or that it's exactly 8 frames across.",
                "OK");
            return;
        }

        // --- Build AnimationClip (12 FPS, no loop) ---
        var clip = new AnimationClip();
        clip.frameRate = 12f;

        // SpriteRenderer.m_Sprite binding
        var binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite"
        };

        var keys = new ObjectReferenceKeyframe[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            keys[i] = new ObjectReferenceKeyframe
            {
                time = i / clip.frameRate,
                value = sprites[i]
            };
        }
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        // Non-looping
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        // Save next to the texture
        string animPath = System.IO.Path.ChangeExtension(path, null); // drop .png
        animPath += "_Attack.anim";
        AssetDatabase.CreateAsset(clip, AssetDatabase.GenerateUniqueAssetPath(animPath));
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Done",
            $"Created animation:\n{animPath}\n\nFrames: {sprites.Length}\nFPS: {clip.frameRate}\nLoop: No\nPivot: Bottom Center",
            "Nice!");
    }
}
