using UnityEditor;
using UnityEngine;

// Правила импорта активов (применяются автоматически при добавлении файлов в проект):
// текстуры — mipmap, анизотропия, сжатие; модели — масштаб 1, нормали из файла, без лишних материалов;
// звук — короткие эффекты в моно с распаковкой при загрузке, музыка и гул — со сжатием Vorbis.
public class AssetImportRules : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Textures/")) return;
        var ti = (TextureImporter)assetImporter;
        string n = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        ti.mipmapEnabled = true;
        ti.anisoLevel = 4;
        ti.maxTextureSize = 1024;
        ti.textureCompression = TextureImporterCompression.Compressed;
        ti.filterMode = FilterMode.Trilinear;
        ti.wrapMode = n == "Tex_Floor" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        bool particle = n == "Tex_Particle" || n == "Tex_Spark" || n == "Tex_Leaf";
        if (particle)
        {
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.maxTextureSize = 128;
        }
    }

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith("Assets/Models/")) return;
        var mi = (ModelImporter)assetImporter;
        mi.globalScale = 1f;
        mi.importNormals = ModelImporterNormals.Import;
        mi.addCollider = false;
        mi.isReadable = false;
        mi.generateSecondaryUV = true;                      // UV для запекания освещения
        mi.materialImportMode = ModelImporterMaterialImportMode.None;
        mi.meshCompression = ModelImporterMeshCompression.Off;
    }

    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/Audio/")) return;
        var ai = (AudioImporter)assetImporter;
        string n = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        bool longClip = n == "music" || n == "ambience";
        var s = ai.defaultSampleSettings;
        s.loadType = longClip ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
        s.compressionFormat = longClip ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
        s.quality = 0.6f;
        ai.defaultSampleSettings = s;
        ai.forceToMono = !longClip || n == "ambience";
        var settings = ai.defaultSampleSettings;
settings.preloadAudioData = true;
ai.defaultSampleSettings = settings;
        ai.loadInBackground = false;
    }
}
