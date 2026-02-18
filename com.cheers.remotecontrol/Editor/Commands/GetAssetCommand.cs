using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Unity.RemoteControl.Editor.Commands
{
    public class GetAssetCommand : ICommand
    {
        public string Name => "get_asset";

        public async Task<Response> ExecuteAsync(Request request)
        {
            var path = request.GetParam<string>("path", null);

            if (string.IsNullOrEmpty(path))
                return Response.Error(request.id, "Missing required parameter: path");

            var result = await MainThreadDispatcher.EnqueueAsync(() =>
            {
                var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (asset == null)
                    return (null, "Asset not found: " + path);

                var so = new SerializedObject(asset);
                var properties = new List<PropertyInfo>();

                var prop = so.GetIterator();
                if (prop.NextVisible(true))
                {
                    do
                    {
                        var propInfo = GetPrefabCommand.SerializeProperty(prop);
                        if (propInfo != null)
                            properties.Add(propInfo);
                    }
                    while (prop.NextVisible(false));
                }

                var info = new AssetInfo
                {
                    name = asset.name,
                    path = path,
                    guid = AssetDatabase.AssetPathToGUID(path),
                    type = asset.GetType().Name,
                    properties = properties
                };

                return (info, (string)null);
            });

            if (result.Item2 != null)
                return Response.Error(request.id, result.Item2);

            return Response.Success(request.id, result.Item1);
        }
    }
}
