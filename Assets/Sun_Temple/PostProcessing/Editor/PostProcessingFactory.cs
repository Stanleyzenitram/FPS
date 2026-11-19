using UnityEngine;
using UnityEngine.PostProcessing;
using System.IO;

namespace UnityEditor.PostProcessing
{
    public class PostProcessingFactory
    {
        [MenuItem("Assets/Create/Post-Processing Profile", priority = 201)]
        static void MenuCreatePostProcessingProfile()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Crear perfil de post-procesado", "New Post-Processing Profile", "asset",
                "Selecciona donde guardar el perfil");
            if (!string.IsNullOrEmpty(path)) CreatePostProcessingProfileAtPath(path);
        }

        internal static PostProcessingProfile CreatePostProcessingProfileAtPath(string path)
        {
            var profile = ScriptableObject.CreateInstance<PostProcessingProfile>();
            profile.name = Path.GetFileName(path);
            profile.fog.enabled = true;
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }
    }
}
