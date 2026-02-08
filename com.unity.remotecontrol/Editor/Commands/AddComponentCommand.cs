using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Unity.RemoteControl.Editor.Commands
{
    public class AddComponentCommand : ICommand
    {
        public string Name => "add_component";

        public async Task<Response> ExecuteAsync(Request request)
        {
            var path = request.GetParam<string>("path", null);
            var gameObjectPath = request.GetParam<string>("gameobject_path", null);
            var componentType = request.GetParam<string>("component_type", null);

            if (string.IsNullOrEmpty(path))
                return Response.Error(request.id, "Missing required parameter: path");

            if (string.IsNullOrEmpty(componentType))
                return Response.Error(request.id, "Missing required parameter: component_type");

            var result = await MainThreadDispatcher.EnqueueAsync(() =>
            {
                var type = GetComponentCommand.FindType(componentType);
                if (type == null)
                    return (null, $"Unknown component type: {componentType}");

                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                    return (null, "Prefab not found");

                try
                {
                    var target = root;
                    if (!string.IsNullOrEmpty(gameObjectPath))
                    {
                        var transform = root.transform.Find(gameObjectPath);
                        if (transform == null)
                            return (null, $"GameObject not found: {gameObjectPath}");
                        target = transform.gameObject;
                    }

                    Undo.RecordObject(target, $"Remote Control: Add {componentType}");

                    var component = target.AddComponent(type);
                    if (component == null)
                        return (null, $"Failed to add component: {componentType}");

                    PrefabUtility.SaveAsPrefabAsset(root, path);

                    var info = GetPrefabCommand.SerializeComponent(component, true);
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
