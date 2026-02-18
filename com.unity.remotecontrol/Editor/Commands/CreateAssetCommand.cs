using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Unity.RemoteControl.Editor.Commands
{
    public class CreateAssetCommand : ICommand
    {
        public string Name => "create_asset";

        public async Task<Response> ExecuteAsync(Request request)
        {
            var path = request.GetParam<string>("path", null);
            var typeName = request.GetParam<string>("type", null);

            if (string.IsNullOrEmpty(path))
                return Response.Error(request.id, "Missing required parameter: path");

            if (string.IsNullOrEmpty(typeName))
                return Response.Error(request.id, "Missing required parameter: type");

            var result = await MainThreadDispatcher.EnqueueAsync(() =>
            {
                // Ensure directory exists
                var directory = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
                {
                    CreateFolderRecursive(directory);
                }

                // Check if asset already exists
                var existing = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (existing != null)
                    return (null, $"Asset already exists at: {path}");

                // Resolve the type
                var type = GetComponentCommand.FindType(typeName);
                if (type == null)
                    return (null, $"Unknown type: {typeName}");

                // Create the asset
                Object asset;
                if (typeof(ScriptableObject).IsAssignableFrom(type))
                {
                    asset = ScriptableObject.CreateInstance(type);
                }
                else
                {
                    // For types like PhysicsMaterial2D, Material, etc.
                    try
                    {
                        asset = (Object)System.Activator.CreateInstance(type);
                    }
                    catch
                    {
                        return (null, $"Cannot create instance of type: {typeName}. Type must be a ScriptableObject or have a parameterless constructor.");
                    }
                }

                if (asset == null)
                    return (null, $"Failed to create instance of type: {typeName}");

                asset.name = System.IO.Path.GetFileNameWithoutExtension(path);

                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();

                var info = new AssetInfo
                {
                    name = asset.name,
                    path = path,
                    guid = AssetDatabase.AssetPathToGUID(path),
                    type = type.Name
                };

                return (info, (string)null);
            });

            if (result.Item2 != null)
                return Response.Error(request.id, result.Item2);

            return Response.Success(request.id, result.Item1);
        }

        private static void CreateFolderRecursive(string path)
        {
            var parts = path.Replace("\\", "/").Split('/');
            var current = parts[0];

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
    }
}
