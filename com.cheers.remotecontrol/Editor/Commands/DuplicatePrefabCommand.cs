using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Unity.RemoteControl.Editor.Commands
{
    public class DuplicatePrefabCommand : ICommand
    {
        public string Name => "duplicate_prefab";

        public async Task<Response> ExecuteAsync(Request request)
        {
            var sourcePath = request.GetParam<string>("source_path", null);
            var destPath = request.GetParam<string>("dest_path", null);

            if (string.IsNullOrEmpty(sourcePath))
                return Response.Error(request.id, "Missing required parameter: source_path");

            if (string.IsNullOrEmpty(destPath))
                return Response.Error(request.id, "Missing required parameter: dest_path");

            if (!destPath.EndsWith(".prefab"))
                destPath += ".prefab";

            var result = await MainThreadDispatcher.EnqueueAsync(() =>
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                if (source == null)
                    return (null, $"Source prefab not found: {sourcePath}");

                if (AssetDatabase.LoadAssetAtPath<GameObject>(destPath) != null)
                    return (null, $"Destination already exists: {destPath}");

                if (!AssetDatabase.CopyAsset(sourcePath, destPath))
                    return (null, "Failed to duplicate prefab");

                AssetDatabase.Refresh();

                var info = new PrefabInfo
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(destPath),
                    path = destPath,
                    guid = AssetDatabase.AssetPathToGUID(destPath)
                };

                return (info, (string)null);
            });

            if (result.Item2 != null)
                return Response.Error(request.id, result.Item2);

            return Response.Success(request.id, result.Item1);
        }
    }
}
