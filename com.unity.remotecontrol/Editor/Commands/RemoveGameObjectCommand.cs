using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Unity.RemoteControl.Editor.Commands
{
    public class RemoveGameObjectCommand : ICommand
    {
        public string Name => "remove_gameobject";

        public async Task<Response> ExecuteAsync(Request request)
        {
            var path = request.GetParam<string>("path", null);
            var gameObjectPath = request.GetParam<string>("gameobject_path", null);

            if (string.IsNullOrEmpty(path))
                return Response.Error(request.id, "Missing required parameter: path");

            if (string.IsNullOrEmpty(gameObjectPath))
                return Response.Error(request.id, "Missing required parameter: gameobject_path (cannot remove root)");

            var result = await MainThreadDispatcher.EnqueueAsync(() =>
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                    return "Prefab not found";

                try
                {
                    var transform = root.transform.Find(gameObjectPath);
                    if (transform == null)
                        return $"GameObject not found: {gameObjectPath}";

                    Undo.DestroyObjectImmediate(transform.gameObject);
                    PrefabUtility.SaveAsPrefabAsset(root, path);

                    return null;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            });

            if (result != null)
                return Response.Error(request.id, result);

            return Response.Success(request.id, new { removed = true });
        }
    }
}
