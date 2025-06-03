#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Source: https://forum.unity.com/threads/callback-when-compute-shader-is-reloaded.726080/ 
// Modified to use a List of Dictionaries

namespace LostInLeaves.WatercolorRendering
{
    class ComputeShaderPostprocessor : AssetPostprocessor
    {
        public delegate void ComputeShaderEventHandler(ComputeShader shader);

        static Dictionary<ComputeShader, List<ComputeShaderEventHandler>> Handlers =
            new Dictionary<ComputeShader, List<ComputeShaderEventHandler>>();

        public static void AddImportHandler(ComputeShader shader, ComputeShaderEventHandler handler)
        {
            if (!Handlers.TryGetValue(shader, out var list))
            {
                list = new List<ComputeShaderEventHandler>();
                Handlers.Add(shader, list);
            }

            list.Add(handler);
        }

        public static void RemoveImportHandler(ComputeShader shader)
        {
            Handlers.Remove(shader);
        }

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (string str in importedAssets)
            {
                if (str.EndsWith(".compute"))
                {
                    var shader = (ComputeShader)AssetDatabase.LoadAssetAtPath(str, typeof(ComputeShader));
                    if (shader != null && Handlers.TryGetValue(shader, out var handler))
                    {
                        foreach (var h in handler)
                        {
                            h(shader);
                        }
                    }
                }
            }
        }
    }

#endif
}