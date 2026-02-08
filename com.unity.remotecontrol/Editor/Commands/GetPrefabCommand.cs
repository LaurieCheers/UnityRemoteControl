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
            var includeProperties = request.GetParam<bool>("include_properties", true);

            if (string.IsNullOrEmpty(path))
                return Response.Error(request.id, "Missing required parameter: path");

            var result = await MainThreadDispatcher.EnqueueAsync(() =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    return null;

                return SerializeGameObject(prefab, "", includeProperties);
            });

            if (result == null)
                return Response.Error(request.id, $"Prefab not found: {path}");

            return Response.Success(request.id, result);
        }

        internal static GameObjectInfo SerializeGameObject(GameObject go, string parentPath, bool includeProperties)
        {
            var currentPath = string.IsNullOrEmpty(parentPath) ? go.name : $"{parentPath}/{go.name}";

            var info = new GameObjectInfo
            {
                name = go.name,
                path = currentPath,
                instanceId = go.GetInstanceID(),
                activeSelf = go.activeSelf,
                tag = go.tag,
                layer = go.layer,
                components = new List<ComponentInfo>(),
                children = new List<GameObjectInfo>()
            };

            // Serialize components
            var components = go.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component == null) continue;

                var compInfo = SerializeComponent(component, includeProperties);
                info.components.Add(compInfo);
            }

            // Serialize children
            for (int i = 0; i < go.transform.childCount; i++)
            {
                var child = go.transform.GetChild(i).gameObject;
                info.children.Add(SerializeGameObject(child, currentPath, includeProperties));
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
                    return obj != null ? obj.name : null;
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
    }
}
