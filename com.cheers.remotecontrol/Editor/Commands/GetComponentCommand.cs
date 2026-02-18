using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Unity.RemoteControl.Editor.Commands
{
    public class GetComponentCommand : ICommand
    {
        public string Name => "get_component";

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

                    Component component = null;
                    if (componentIndex >= 0)
                    {
                        var components = target.GetComponents<Component>();
                        if (componentIndex >= components.Length)
                            return (null, $"Component index out of range: {componentIndex}");
                        component = components[componentIndex];
                    }
                    else
                    {
                        var type = FindType(componentType);
                        if (type == null)
                            return (null, $"Unknown component type: {componentType}");
                        component = target.GetComponent(type);
                    }

                    if (component == null)
                        return (null, "Component not found");

                    return (GetPrefabCommand.SerializeComponent(component, true), (string)null);
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

        internal static System.Type FindType(string typeName)
        {
            // Try direct lookup first
            var type = System.Type.GetType(typeName);
            if (type != null)
                return type;

            // Check UnityEngine namespace
            type = System.Type.GetType($"UnityEngine.{typeName}, UnityEngine.CoreModule");
            if (type != null)
                return type;

            type = System.Type.GetType($"UnityEngine.{typeName}, UnityEngine");
            if (type != null)
                return type;

            // Search all loaded assemblies
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null)
                    return type;

                type = assembly.GetType($"UnityEngine.{typeName}");
                if (type != null)
                    return type;
            }

            return null;
        }
    }
}
