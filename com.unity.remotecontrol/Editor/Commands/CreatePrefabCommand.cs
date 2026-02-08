using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Unity.RemoteControl.Editor.Commands
{
    public class CreatePrefabCommand : ICommand
    {
        public string Name => "create_prefab";

        public async Task<Response> ExecuteAsync(Request request)
        {
            var path = request.GetParam<string>("path", null);
            var name = request.GetParam<string>("name", null);
            var primitiveType = request.GetParam<string>("primitive", null);

            if (string.IsNullOrEmpty(path))
                return Response.Error(request.id, "Missing required parameter: path");

            // Ensure path ends with .prefab
            if (!path.EndsWith(".prefab"))
                path += ".prefab";

            // Extract name from path if not provided
            if (string.IsNullOrEmpty(name))
                name = System.IO.Path.GetFileNameWithoutExtension(path);

            var result = await MainThreadDispatcher.EnqueueAsync(() =>
            {
                // Ensure directory exists
                var directory = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
                {
                    CreateFolderRecursive(directory);
                }

                // Check if prefab already exists
                var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (existingPrefab != null)
                    return (null, $"Prefab already exists at: {path}");

                // Create the root GameObject
                GameObject go;
                if (!string.IsNullOrEmpty(primitiveType))
                {
                    var primitive = ParsePrimitiveType(primitiveType);
                    if (primitive.HasValue)
                    {
                        go = GameObject.CreatePrimitive(primitive.Value);
                        go.name = name;
                    }
                    else
                    {
                        return (null, $"Unknown primitive type: {primitiveType}. Valid types: Cube, Sphere, Capsule, Cylinder, Plane, Quad");
                    }
                }
                else
                {
                    go = new GameObject(name);
                }

                try
                {
                    // Save as prefab
                    var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
                    if (prefab == null)
                        return (null, "Failed to create prefab");

                    var info = new PrefabInfo
                    {
                        name = prefab.name,
                        path = path,
                        guid = AssetDatabase.AssetPathToGUID(path)
                    };

                    return (info, (string)null);
                }
                finally
                {
                    // Clean up the temporary GameObject
                    Object.DestroyImmediate(go);
                }
            });

            if (result.Item2 != null)
                return Response.Error(request.id, result.Item2);

            return Response.Success(request.id, result.Item1);
        }

        private static void CreateFolderRecursive(string path)
        {
            var parts = path.Replace("\\", "/").Split('/');
            var current = parts[0]; // "Assets"

            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private static PrimitiveType? ParsePrimitiveType(string type)
        {
            switch (type.ToLowerInvariant())
            {
                case "cube": return PrimitiveType.Cube;
                case "sphere": return PrimitiveType.Sphere;
                case "capsule": return PrimitiveType.Capsule;
                case "cylinder": return PrimitiveType.Cylinder;
                case "plane": return PrimitiveType.Plane;
                case "quad": return PrimitiveType.Quad;
                default: return null;
            }
        }
    }
}
