using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Unity.RemoteControl.Editor.Commands
{
    public class SetAssetPropertyCommand : ICommand
    {
        public string Name => "set_asset_property";

        public async Task<Response> ExecuteAsync(Request request)
        {
            var path = request.GetParam<string>("path", null);
            var propertyPath = request.GetParam<string>("property_path", null);
            var value = request.@params?.ContainsKey("value") == true ? request.@params["value"] : null;

            if (string.IsNullOrEmpty(path))
                return Response.Error(request.id, "Missing required parameter: path");

            if (string.IsNullOrEmpty(propertyPath))
                return Response.Error(request.id, "Missing required parameter: property_path");

            var result = await MainThreadDispatcher.EnqueueAsync(() =>
            {
                var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (asset == null)
                    return "Asset not found: " + path;

                var so = new SerializedObject(asset);
                var prop = so.FindProperty(propertyPath);

                if (prop == null)
                    return $"Property not found: {propertyPath}";

                if (!prop.editable)
                    return $"Property is read-only: {propertyPath}";

                Undo.RecordObject(asset, $"Remote Control: Set {propertyPath}");

                var setResult = SetPropertyCommand.SetPropertyValue(prop, value);
                if (setResult != null)
                    return setResult;

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();

                return null;
            });

            if (result != null)
                return Response.Error(request.id, result);

            return Response.Success(request.id, new { modified = true });
        }
    }
}
