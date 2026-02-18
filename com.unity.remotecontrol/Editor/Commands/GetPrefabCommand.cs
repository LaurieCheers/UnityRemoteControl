using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Unity.RemoteControl.Editor.Commands
{
    public class GetPrefabCommand : ICommand
    {
        public string Name => "get_prefab";

        public async Task<Response> ExecuteAsync(Request request)
        {
            var path = request.GetParam<string>("path", null);
            var includeProperties = request.GetParam<bool>("include_properties", false);
            var maxDepth = request.GetParam<int>("max_depth", -1);
            var gameObjectPath = request.GetParam<string>("gameobject_path", null);

            if (string.IsNullOrEmpty(path))
                return Response.Error(request.id, "Missing required parameter: path");

            var result = await MainThreadDispatcher.EnqueueAsync(() =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    return (null, "Prefab not found: " + path);

                GameObject target = prefab;
                if (!string.IsNullOrEmpty(gameObjectPath))
                {
                    var transform = prefab.transform.Find(gameObjectPath);
                    if (transform == null)
                        return (null, "GameObject not found: " + gameObjectPath);
                    target = transform.gameObject;
                }

                var parentPath = "";
                if (!string.IsNullOrEmpty(gameObjectPath))
                {
                    // Build parent path up to but not including the target
                    var lastSlash = gameObjectPath.LastIndexOf('/');
                    if (lastSlash >= 0)
                        parentPath = prefab.name + "/" + gameObjectPath.Substring(0, lastSlash);
                    else
                        parentPath = prefab.name;
                }

                return (SerializeGameObject(target, parentPath, includeProperties, maxDepth, 0), (string)null);
            });

            if (result.Item2 != null)
                return Response.Error(request.id, result.Item2);

            return Response.Success(request.id, result.Item1);
        }

        internal static GameObjectInfo SerializeGameObject(GameObject go, string parentPath, bool includeProperties, int maxDepth, int currentDepth)
        {
            var currentPath = string.IsNullOrEmpty(parentPath) ? go.name : $"{parentPath}/{go.name}";

            // Focus node (depth 0 with properties): full detail
            // Overview nodes: just name, path, component names, child counts
            bool isFocusNode = includeProperties && currentDepth == 0;

            var info = new GameObjectInfo
            {
                name = go.name,
                path = currentPath,
                children = new List<GameObjectInfo>(),
                childCount = go.transform.childCount
            };

            if (isFocusNode)
            {
                info.instanceId = go.GetInstanceID();
                info.activeSelf = go.activeSelf;
                info.tag = go.tag;
                info.layer = go.layer;
                info.components = new List<ComponentInfo>();
                var components = go.GetComponents<Component>();
                foreach (var component in components)
                {
                    if (component == null) continue;
                    info.components.Add(SerializeComponent(component, true));
                }
            }
            else
            {
                info.componentNames = new List<string>();
                var components = go.GetComponents<Component>();
                foreach (var component in components)
                {
                    if (component == null) continue;
                    var typeName = component.GetType().Name;
                    if (typeName == "Transform" || typeName == "RectTransform") continue;
                    info.componentNames.Add(typeName);
                }
            }

            // Recurse into children if within depth limit
            if (maxDepth < 0 || currentDepth < maxDepth)
            {
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    var child = go.transform.GetChild(i).gameObject;
                    info.children.Add(SerializeGameObject(child, currentPath, includeProperties, maxDepth, currentDepth + 1));
                }
            }

            return info;
        }

        internal static ComponentInfo SerializeComponent(Component component, bool includeProperties)
        {
            var type = component.GetType();
            var compInfo = new ComponentInfo
            {
                type = type.Name,
                fullType = type.FullName,
                instanceId = component.GetInstanceID(),
                enabled = !(component is Behaviour behaviour) || behaviour.enabled,
                properties = new List<PropertyInfo>()
            };

            if (includeProperties)
            {
                var so = new SerializedObject(component);
                var prop = so.GetIterator();

                if (prop.NextVisible(true))
                {
                    do
                    {
                        var propInfo = SerializeProperty(prop);
                        if (propInfo != null)
                            compInfo.properties.Add(propInfo);
                    }
                    while (prop.NextVisible(false));
                }
            }

            return compInfo;
        }

        internal static PropertyInfo SerializeProperty(SerializedProperty prop)
        {
            var info = new PropertyInfo
            {
                name = prop.name,
                path = prop.propertyPath,
                type = prop.propertyType.ToString(),
                isReadOnly = !prop.editable,
                isArray = prop.isArray,
                arraySize = prop.isArray ? prop.arraySize : 0
            };

            info.value = GetPropertyValue(prop);

            return info;
        }

        internal static object GetPropertyValue(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return prop.intValue;
                case SerializedPropertyType.Boolean:
                    return prop.boolValue;
                case SerializedPropertyType.Float:
                    return prop.floatValue;
                case SerializedPropertyType.String:
                    return prop.stringValue;
                case SerializedPropertyType.Color:
                    var c = prop.colorValue;
                    return new float[] { c.r, c.g, c.b, c.a };
                case SerializedPropertyType.ObjectReference:
                    var obj = prop.objectReferenceValue;
                    if (obj == null) return null;
                    var objAssetPath = AssetDatabase.GetAssetPath(obj);

                    // If it's a standalone asset (not inside a prefab/scene), return the asset path
                    if (!string.IsNullOrEmpty(objAssetPath))
                    {
                        // Check if the object IS the main asset (not something inside a prefab)
                        var mainAsset = AssetDatabase.LoadMainAssetAtPath(objAssetPath);
                        if (mainAsset == obj || !(obj is GameObject || obj is Component))
                            return objAssetPath;
                    }

                    // Internal reference: build a path like "Body/LeftArm" or "Body/LeftArm:BoxCollider"
                    if (obj is GameObject go)
                    {
                        return GetGameObjectPath(go);
                    }
                    else if (obj is Component comp)
                    {
                        var goPath = GetGameObjectPath(comp.gameObject);
                        return $"{goPath}:{comp.GetType().Name}";
                    }

                    return obj.name;
                case SerializedPropertyType.Enum:
                    return prop.enumNames.Length > prop.enumValueIndex && prop.enumValueIndex >= 0
                        ? prop.enumNames[prop.enumValueIndex]
                        : prop.enumValueIndex.ToString();
                case SerializedPropertyType.Vector2:
                    var v2 = prop.vector2Value;
                    return new float[] { v2.x, v2.y };
                case SerializedPropertyType.Vector3:
                    var v3 = prop.vector3Value;
                    return new float[] { v3.x, v3.y, v3.z };
                case SerializedPropertyType.Vector4:
                    var v4 = prop.vector4Value;
                    return new float[] { v4.x, v4.y, v4.z, v4.w };
                case SerializedPropertyType.Rect:
                    var r = prop.rectValue;
                    return new float[] { r.x, r.y, r.width, r.height };
                case SerializedPropertyType.Bounds:
                    var b = prop.boundsValue;
                    return new float[] { b.center.x, b.center.y, b.center.z, b.size.x, b.size.y, b.size.z };
                case SerializedPropertyType.Quaternion:
                    var q = prop.quaternionValue;
                    return new float[] { q.x, q.y, q.z, q.w };
                case SerializedPropertyType.Vector2Int:
                    var v2i = prop.vector2IntValue;
                    return new int[] { v2i.x, v2i.y };
                case SerializedPropertyType.Vector3Int:
                    var v3i = prop.vector3IntValue;
                    return new int[] { v3i.x, v3i.y, v3i.z };
                case SerializedPropertyType.RectInt:
                    var ri = prop.rectIntValue;
                    return new int[] { ri.x, ri.y, ri.width, ri.height };
                case SerializedPropertyType.BoundsInt:
                    var bi = prop.boundsIntValue;
                    return new int[] { bi.x, bi.y, bi.z, bi.size.x, bi.size.y, bi.size.z };
                default:
                    return null;
            }
        }

        /// <summary>
        /// Build a hierarchy path for a GameObject relative to its root.
        /// Returns the path suitable for Transform.Find() (excludes root name).
        /// If the object IS the root, returns its name.
        /// </summary>
        private static string GetGameObjectPath(GameObject go)
        {
            var parts = new List<string>();
            var current = go.transform;
            while (current != null)
            {
                parts.Add(current.name);
                current = current.parent;
            }
            parts.Reverse();

            // If only one part, it's the root itself
            if (parts.Count <= 1)
                return go.name;

            // Skip root name, return relative path (matches Transform.Find format)
            return string.Join("/", parts.GetRange(1, parts.Count - 1));
        }
    }
}
