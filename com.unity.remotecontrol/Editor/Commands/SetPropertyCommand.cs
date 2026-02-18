using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace Unity.RemoteControl.Editor.Commands
{
    public class SetPropertyCommand : ICommand
    {
        public string Name => "set_property";

        public async Task<Response> ExecuteAsync(Request request)
        {
            var path = request.GetParam<string>("path", null);
            var gameObjectPath = request.GetParam<string>("gameobject_path", null);
            var componentType = request.GetParam<string>("component_type", null);
            var componentIndex = request.GetParam<int>("component_index", -1);
            var propertyPath = request.GetParam<string>("property_path", null);
            var value = request.@params?.ContainsKey("value") == true ? request.@params["value"] : null;

            if (string.IsNullOrEmpty(path))
                return Response.Error(request.id, "Missing required parameter: path");

            if (string.IsNullOrEmpty(componentType) && componentIndex < 0)
                return Response.Error(request.id, "Must specify either component_type or component_index");

            if (string.IsNullOrEmpty(propertyPath))
                return Response.Error(request.id, "Missing required parameter: property_path");

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

                    var so = new SerializedObject(component);
                    var prop = so.FindProperty(propertyPath);

                    if (prop == null)
                        return $"Property not found: {propertyPath}";

                    if (!prop.editable)
                        return $"Property is read-only: {propertyPath}";

                    Undo.RecordObject(component, $"Remote Control: Set {propertyPath}");

                    var setResult = SetPropertyValue(prop, value, root);
                    if (setResult != null)
                        return setResult;

                    so.ApplyModifiedProperties();
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

            return Response.Success(request.id, new { modified = true });
        }

        internal static string SetPropertyValue(SerializedProperty prop, object value, GameObject prefabRoot = null)
        {
            try
            {
                switch (prop.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        prop.intValue = System.Convert.ToInt32(value);
                        break;

                    case SerializedPropertyType.Boolean:
                        prop.boolValue = System.Convert.ToBoolean(value);
                        break;

                    case SerializedPropertyType.Float:
                        prop.floatValue = System.Convert.ToSingle(value);
                        break;

                    case SerializedPropertyType.String:
                        prop.stringValue = value?.ToString() ?? "";
                        break;

                    case SerializedPropertyType.Enum:
                        if (value is string enumName)
                        {
                            var index = System.Array.IndexOf(prop.enumNames, enumName);
                            if (index >= 0)
                                prop.enumValueIndex = index;
                            else
                                return $"Invalid enum value: {enumName}";
                        }
                        else
                        {
                            prop.enumValueIndex = System.Convert.ToInt32(value);
                        }
                        break;

                    case SerializedPropertyType.Color:
                        var colorArr = ParseFloatArray(value);
                        if (colorArr == null || colorArr.Length < 3)
                            return "Color requires [r, g, b] or [r, g, b, a] array";
                        prop.colorValue = new Color(
                            colorArr[0],
                            colorArr[1],
                            colorArr[2],
                            colorArr.Length > 3 ? colorArr[3] : 1f
                        );
                        break;

                    case SerializedPropertyType.Vector2:
                        var v2Arr = ParseFloatArray(value);
                        if (v2Arr == null || v2Arr.Length < 2)
                            return "Vector2 requires [x, y] array";
                        prop.vector2Value = new Vector2(v2Arr[0], v2Arr[1]);
                        break;

                    case SerializedPropertyType.Vector3:
                        var v3Arr = ParseFloatArray(value);
                        if (v3Arr == null || v3Arr.Length < 3)
                            return "Vector3 requires [x, y, z] array";
                        prop.vector3Value = new Vector3(v3Arr[0], v3Arr[1], v3Arr[2]);
                        break;

                    case SerializedPropertyType.Vector4:
                        var v4Arr = ParseFloatArray(value);
                        if (v4Arr == null || v4Arr.Length < 4)
                            return "Vector4 requires [x, y, z, w] array";
                        prop.vector4Value = new Vector4(v4Arr[0], v4Arr[1], v4Arr[2], v4Arr[3]);
                        break;

                    case SerializedPropertyType.Quaternion:
                        var qArr = ParseFloatArray(value);
                        if (qArr == null || qArr.Length < 4)
                            return "Quaternion requires [x, y, z, w] array";
                        prop.quaternionValue = new Quaternion(qArr[0], qArr[1], qArr[2], qArr[3]);
                        break;

                    case SerializedPropertyType.Rect:
                        var rArr = ParseFloatArray(value);
                        if (rArr == null || rArr.Length < 4)
                            return "Rect requires [x, y, width, height] array";
                        prop.rectValue = new Rect(rArr[0], rArr[1], rArr[2], rArr[3]);
                        break;

                    case SerializedPropertyType.Vector2Int:
                        var v2iArr = ParseIntArray(value);
                        if (v2iArr == null || v2iArr.Length < 2)
                            return "Vector2Int requires [x, y] array";
                        prop.vector2IntValue = new Vector2Int(v2iArr[0], v2iArr[1]);
                        break;

                    case SerializedPropertyType.Vector3Int:
                        var v3iArr = ParseIntArray(value);
                        if (v3iArr == null || v3iArr.Length < 3)
                            return "Vector3Int requires [x, y, z] array";
                        prop.vector3IntValue = new Vector3Int(v3iArr[0], v3iArr[1], v3iArr[2]);
                        break;

                    case SerializedPropertyType.RectInt:
                        var riArr = ParseIntArray(value);
                        if (riArr == null || riArr.Length < 4)
                            return "RectInt requires [x, y, width, height] array";
                        prop.rectIntValue = new RectInt(riArr[0], riArr[1], riArr[2], riArr[3]);
                        break;

                    case SerializedPropertyType.ObjectReference:
                        var assetPath = value?.ToString();
                        if (string.IsNullOrEmpty(assetPath))
                        {
                            prop.objectReferenceValue = null;
                            break;
                        }

                        Object resolvedObj = null;

                        // Check for internal prefab reference: "go:ComponentType" or "go" (for GameObject)
                        if (prefabRoot != null && !assetPath.StartsWith("Assets/"))
                        {
                            resolvedObj = ResolveInternalReference(prefabRoot, assetPath);
                        }

                        // Try loading by asset path
                        if (resolvedObj == null)
                        {
                            resolvedObj = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                        }

                        // Try searching by name as fallback
                        if (resolvedObj == null)
                        {
                            var guids = AssetDatabase.FindAssets(assetPath);
                            if (guids.Length == 1)
                            {
                                var foundPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                                resolvedObj = AssetDatabase.LoadAssetAtPath<Object>(foundPath);
                            }
                            else if (guids.Length > 1)
                            {
                                return $"Ambiguous asset reference '{assetPath}' - found {guids.Length} matches. Use the full asset path instead.";
                            }
                        }

                        if (resolvedObj == null)
                            return $"Object not found: {assetPath}. For assets use full path (Assets/...). For prefab internals use gameobject path or path:ComponentType";
                        prop.objectReferenceValue = resolvedObj;
                        break;

                    default:
                        return $"Property type {prop.propertyType} is not supported for modification";
                }

                return null;
            }
            catch (System.Exception ex)
            {
                return $"Failed to set property: {ex.Message}";
            }
        }

        private static float[] ParseFloatArray(object value)
        {
            if (value is string jsonArray)
            {
                jsonArray = jsonArray.Trim();
                if (jsonArray.StartsWith("[") && jsonArray.EndsWith("]"))
                {
                    var inner = jsonArray.Substring(1, jsonArray.Length - 2);
                    var parts = inner.Split(',');
                    var result = new float[parts.Length];
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (!float.TryParse(parts[i].Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out result[i]))
                            return null;
                    }
                    return result;
                }
            }

            if (value is System.Collections.IList list)
            {
                var result = new float[list.Count];
                for (int i = 0; i < list.Count; i++)
                {
                    result[i] = System.Convert.ToSingle(list[i]);
                }
                return result;
            }

            return null;
        }

        private static int[] ParseIntArray(object value)
        {
            if (value is string jsonArray)
            {
                jsonArray = jsonArray.Trim();
                if (jsonArray.StartsWith("[") && jsonArray.EndsWith("]"))
                {
                    var inner = jsonArray.Substring(1, jsonArray.Length - 2);
                    var parts = inner.Split(',');
                    var result = new int[parts.Length];
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (!int.TryParse(parts[i].Trim(), out result[i]))
                            return null;
                    }
                    return result;
                }
            }

            if (value is System.Collections.IList list)
            {
                var result = new int[list.Count];
                for (int i = 0; i < list.Count; i++)
                {
                    result[i] = System.Convert.ToInt32(list[i]);
                }
                return result;
            }

            return null;
        }

        /// <summary>
        /// Resolve an internal prefab reference like "Body/LeftArm" (GameObject)
        /// or "Body/LeftArm:BoxCollider" (Component on a child).
        /// Plain name like "LeftArm" also works for direct children.
        /// </summary>
        private static Object ResolveInternalReference(GameObject root, string reference)
        {
            string goPath;
            string componentType = null;

            // Split on last ':' to separate path from component type
            var colonIdx = reference.LastIndexOf(':');
            if (colonIdx >= 0)
            {
                goPath = reference.Substring(0, colonIdx);
                componentType = reference.Substring(colonIdx + 1);
            }
            else
            {
                goPath = reference;
            }

            // Find the target GameObject
            GameObject target = null;
            if (string.IsNullOrEmpty(goPath) || goPath == root.name)
            {
                target = root;
            }
            else
            {
                var transform = root.transform.Find(goPath);
                if (transform != null)
                    target = transform.gameObject;
            }

            if (target == null)
                return null;

            // If no component type specified, return the GameObject itself
            if (string.IsNullOrEmpty(componentType))
                return target;

            // Find the component by type name
            var type = GetComponentCommand.FindType(componentType);
            if (type != null)
                return target.GetComponent(type);

            // Fallback: search by name
            foreach (var comp in target.GetComponents<Component>())
            {
                if (comp != null && comp.GetType().Name == componentType)
                    return comp;
            }

            return null;
        }
    }
}
