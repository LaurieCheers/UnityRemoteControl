using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Unity.RemoteControl.Editor.Commands
{
    public class AddGameObjectCommand : ICommand
    {
        public string Name => "add_gameobject";

        public async Task<Response> ExecuteAsync(Request request)
        {
            var path = request.GetParam<string>("path", null);
            var parentPath = request.GetParam<string>("parent_path", null);
            var name = request.GetParam<string>("name", "New GameObject");

            if (string.IsNullOrEmpty(path))
                return Response.Error(request.id, "Missing required parameter: path");

            var result = await MainThreadDispatcher.EnqueueAsync(() =>
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                    return (null, "Prefab not found");

                try
                {
                    Transform parent = root.transform;
                    if (!string.IsNullOrEmpty(parentPath))
                    {
                        parent = root.transform.Find(parentPath);
                        if (parent == null)
                            return (null, $"Parent not found: {parentPath}");
                    }

                    var newGo = new GameObject(name);
                    Undo.RegisterCreatedObjectUndo(newGo, $"Remote Control: Add {name}");

                    newGo.transform.SetParent(parent, false);
                    newGo.transform.localPosition = Vector3.zero;
                    newGo.transform.localRotation = Quaternion.identity;
                    newGo.transform.localScale = Vector3.one;

                    PrefabUtility.SaveAsPrefabAsset(root, path);

                    var info = GetPrefabCommand.SerializeGameObject(newGo, parentPath ?? "", false, -1, 0);
                    return (info, (string)null);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            });

            if (result.Item2 != null)
                return Response.Error(request.id, result.Item2);

            return Response.Success(request.id, result.Item1);
        }
    }
}
