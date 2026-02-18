using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.RemoteControl
{
    [Serializable]
    public class Request
    {
        public string id;
        public string command;
        public Dictionary<string, object> @params;

        public T GetParam<T>(string key, T defaultValue = default)
        {
            if (@params == null || !@params.TryGetValue(key, out var value))
                return defaultValue;

            if (value is T typed)
                return typed;

            try
            {
                if (typeof(T) == typeof(int) && value is long longVal)
                    return (T)(object)(int)longVal;
                if (typeof(T) == typeof(float) && value is double doubleVal)
                    return (T)(object)(float)doubleVal;
                if (typeof(T) == typeof(int) && value is double dVal)
                    return (T)(object)(int)dVal;

                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        public bool HasParam(string key)
        {
            return @params != null && @params.ContainsKey(key);
        }
    }

    [Serializable]
    public class Response
    {
        public string id;
        public bool success;
        public object data;
        public string error;

        public static Response Success(string id, object data = null)
        {
            return new Response
            {
                id = id,
                success = true,
                data = data,
                error = null
            };
        }

        public static Response Error(string id, string error)
        {
            return new Response
            {
                id = id,
                success = false,
                data = null,
                error = error
            };
        }
    }

    [Serializable]
    public class GameObjectInfo
    {
        public string name;
        public string path;
        public int instanceId;
        public bool activeSelf;
        public string tag;
        public int layer;
        public List<string> componentNames;
        public List<ComponentInfo> components;
        public List<GameObjectInfo> children;
        public int childCount;
    }

    [Serializable]
    public class ComponentInfo
    {
        public string type;
        public string fullType;
        public int instanceId;
        public bool enabled;
        public List<PropertyInfo> properties;
    }

    [Serializable]
    public class PropertyInfo
    {
        public string name;
        public string path;
        public string type;
        public object value;
        public bool isReadOnly;
        public bool isArray;
        public int arraySize;
    }

    [Serializable]
    public class PrefabInfo
    {
        public string name;
        public string path;
        public string guid;
    }

    [Serializable]
    public class PrefabListResult
    {
        public int total;
        public int offset;
        public int limit;
        public List<PrefabInfo> prefabs;
    }

    [Serializable]
    public class AssetInfo
    {
        public string name;
        public string path;
        public string guid;
        public string type;
        public List<PropertyInfo> properties;
    }

    [Serializable]
    public class AssetListResult
    {
        public int total;
        public int offset;
        public int limit;
        public List<AssetInfo> assets;
    }
}
