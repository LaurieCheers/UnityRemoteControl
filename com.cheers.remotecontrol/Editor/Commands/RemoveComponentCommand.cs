using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Unity.RemoteControl.Editor.Commands
{
    public class RemoveComponentCommand : ICommand
    {
        public string Name => "remove_component";

        public async Task<Response> ExecuteAsync(Request request)
        {
            var path = request.GetParam<string>("path", null);
            var gameObjectPath = request.GetParam<string>("gameobject_path", null);
            var componentType = request.GetParam<string>("component_type", null);
            var componentIndex = request.GetParam<int>("component_index", -1);

            if (string.IsNullOrEmpty(path))
                return Response.Error(request.id, "Missing required parameter: path");

            if (string.IsNullOrEmpty(componentType) && componentIndex < 0)
                return Response.Error(request.id, "Must specify either component_type or component_index");

            var result = await MainThreadDispatcher.EnqueueAsync(() =>
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                    return "Prefab not found";

                try
                {
                    var target = root;
                    if (!string.IsNullOrEmpty(gameObjectPath))
                    {
                        var transform = root.transform.Find(gameObjectPath);
                        if (transform == null)
                            return $"GameObject not found: {gameObjectPath}";
                        target = transform.gameObject;
                    }

                    Component component = null;
                    if (componentIndex >= 0)
                    {
                        var components = target.GetComponents<Component>();
                        if (componentIndex >= components.Length)
                            return $"Component index out of range: {componentIndex}";
                        component = components[componentIndex];
                    }
                    else
                    {
                        var type = GetComponentCommand.FindType(componentType);
                        if (type == null)
                            return $"Unknown component type: {componentType}";
                        component = target.GetComponent(type);
                    }

                    if (component == null)
                        return "Component not found";

                    if (component is Transform)
                        return "Cannot remove Transform component";

                    Undo.DestroyObjectImmediate(component);
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
